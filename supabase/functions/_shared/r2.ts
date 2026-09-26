import {
  DeleteObjectCommand,
  GetObjectCommand,
  HeadObjectCommand,
  ListObjectsV2Command,
  PutObjectCommand,
  S3Client,
} from 'npm:@aws-sdk/client-s3@3.1141.0'
import { getSignedUrl } from 'npm:@aws-sdk/s3-request-presigner@3.1141.0'

/**
 * Cloudflare R2 access (S3-compatible). Credentials live only in Edge Function secrets:
 * R2_ACCOUNT_ID, R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY, R2_BUCKET.
 */
export type R2 = { client: S3Client; bucket: string }

export function r2(): R2 | null {
  const accountId = Deno.env.get('R2_ACCOUNT_ID')
  const accessKeyId = Deno.env.get('R2_ACCESS_KEY_ID')
  const secretAccessKey = Deno.env.get('R2_SECRET_ACCESS_KEY')
  const bucket = Deno.env.get('R2_BUCKET')
  if (!accountId || !accessKeyId || !secretAccessKey || !bucket) return null

  const client = new S3Client({
    region: 'auto',
    endpoint: `https://${accountId}.r2.cloudflarestorage.com`,
    credentials: { accessKeyId, secretAccessKey },
    // Newer AWS SDKs add CRC checksums to every PUT by default, which presigned R2 uploads can't satisfy.
    requestChecksumCalculation: 'WHEN_REQUIRED',
    responseChecksumValidation: 'WHEN_REQUIRED',
  })
  return { client, bucket }
}

/**
 * Presigned PUT for exactly this key, content type and size. Content-Type and Content-Length are
 * part of the signature, so the upload fails if the client sends anything else.
 */
export function presignUpload({ client, bucket }: R2, key: string, sizeBytes: number, expiresIn = 600) {
  const command = new PutObjectCommand({
    Bucket: bucket,
    Key: key,
    ContentType: 'application/zip',
    ContentLength: sizeBytes,
  })
  return getSignedUrl(client, command, {
    expiresIn,
    signableHeaders: new Set(['content-type', 'content-length']),
  })
}

/** Presigned GET that downloads as an attachment with a friendly file name. */
export function presignDownload({ client, bucket }: R2, key: string, fileName: string, expiresIn = 60) {
  const command = new GetObjectCommand({
    Bucket: bucket,
    Key: key,
    ResponseContentDisposition: `attachment; filename="${fileName}"`,
    ResponseContentType: 'application/zip',
  })
  return getSignedUrl(client, command, { expiresIn })
}

/** Size and upload time of an object, or null if it doesn't exist. */
export async function head({ client, bucket }: R2, key: string) {
  try {
    const res = await client.send(new HeadObjectCommand({ Bucket: bucket, Key: key }))
    return { size: res.ContentLength ?? 0, lastModified: res.LastModified ?? new Date() }
  } catch (e) {
    const status = (e as { $metadata?: { httpStatusCode?: number } }).$metadata?.httpStatusCode
    if (status === 404 || (e as Error).name === 'NotFound') return null
    throw e
  }
}

/** Reads byte ranges of an object (for peeking inside the zip without downloading all of it). */
export function rangeReader({ client, bucket }: R2, key: string) {
  return async (start: number, end: number) => {
    const res = await client.send(new GetObjectCommand({ Bucket: bucket, Key: key, Range: `bytes=${start}-${end}` }))
    if (!res.Body) throw new Error(`empty range response for ${key}`)
    return await res.Body.transformToByteArray()
  }
}

/** Deletes an object. Deleting something that's already gone is fine. */
export async function remove({ client, bucket }: R2, key: string) {
  await client.send(new DeleteObjectCommand({ Bucket: bucket, Key: key }))
}

/** Every object in the bucket (small bucket: one zip per user). */
export async function* listAll({ client, bucket }: R2) {
  let token: string | undefined
  do {
    const page = await client.send(new ListObjectsV2Command({ Bucket: bucket, ContinuationToken: token }))
    for (const obj of page.Contents ?? []) {
      if (obj.Key) yield { key: obj.Key, size: obj.Size ?? 0, lastModified: obj.LastModified ?? new Date(0) }
    }
    token = page.IsTruncated ? page.NextContinuationToken : undefined
  } while (token)
}
