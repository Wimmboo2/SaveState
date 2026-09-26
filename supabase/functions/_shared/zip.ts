// Reads one entry out of a zip without downloading the whole thing: the end-of-central-directory
// record, then the central directory, then just that entry's bytes. Plain Web APIs only
// (DataView, DecompressionStream), so it runs the same in Deno and in Node tests.

/** Returns bytes [start, end] (inclusive) of the zip. */
export type RangeReader = (start: number, end: number) => Promise<Uint8Array>

const EOCD_SIG = 0x06054b50
const ZIP64_LOCATOR_SIG = 0x07064b50
const ZIP64_EOCD_SIG = 0x06064b50
const CENTRAL_SIG = 0x02014b50
const LOCAL_SIG = 0x04034b50

const EOCD_MIN = 22
const MAX_COMMENT = 0xffff
const MAX_CENTRAL_DIRECTORY = 16 * 1024 * 1024

export class ZipFormatError extends Error {}

/**
 * The uncompressed bytes of the entry called `name`, or null if the zip has no such entry.
 * Throws ZipFormatError for anything that isn't a sane zip, or if the entry is over `maxBytes`.
 */
export async function readZipEntry(
  read: RangeReader,
  zipSize: number,
  name: string,
  maxBytes = 32 * 1024 * 1024,
): Promise<Uint8Array | null> {
  if (zipSize < EOCD_MIN) throw new ZipFormatError('too small to be a zip')

  // 1. End of central directory: in the last 22 bytes, plus up to 64 KB of comment.
  const tailStart = Math.max(0, zipSize - EOCD_MIN - MAX_COMMENT - 20)
  const tail = await read(tailStart, zipSize - 1)
  const tv = view(tail)
  let eocd = -1
  for (let i = tail.length - EOCD_MIN; i >= 0; i--) {
    if (tv.getUint32(i, true) === EOCD_SIG) {
      eocd = i
      break
    }
  }
  if (eocd < 0) throw new ZipFormatError('no end of central directory')

  let entries = tv.getUint16(eocd + 10, true)
  let cdSize = tv.getUint32(eocd + 12, true)
  let cdOffset = tv.getUint32(eocd + 16, true)

  // Zip64 (more than 65,535 entries): the real numbers are in the zip64 record.
  if (entries === 0xffff || cdSize === 0xffffffff || cdOffset === 0xffffffff) {
    const loc = eocd - 20
    if (loc < 0 || tv.getUint32(loc, true) !== ZIP64_LOCATOR_SIG) throw new ZipFormatError('missing zip64 locator')
    const recordOffset = u64(tv, loc + 8)
    const record = view(await read(recordOffset, recordOffset + 55))
    if (record.getUint32(0, true) !== ZIP64_EOCD_SIG) throw new ZipFormatError('bad zip64 record')
    entries = u64(record, 32)
    cdSize = u64(record, 40)
    cdOffset = u64(record, 48)
  }
  if (cdSize > MAX_CENTRAL_DIRECTORY || cdOffset + cdSize > zipSize) throw new ZipFormatError('central directory out of range')

  // 2. Central directory: find the entry.
  const cd = cdSize === 0 ? new Uint8Array(0) : await read(cdOffset, cdOffset + cdSize - 1)
  const cv = view(cd)
  const decoder = new TextDecoder()
  let p = 0
  for (let n = 0; n < entries; n++) {
    if (p + 46 > cd.length || cv.getUint32(p, true) !== CENTRAL_SIG) throw new ZipFormatError('bad central directory entry')
    const method = cv.getUint16(p + 10, true)
    const compressed = cv.getUint32(p + 20, true)
    const size = cv.getUint32(p + 24, true)
    const nameLen = cv.getUint16(p + 28, true)
    const extraLen = cv.getUint16(p + 30, true)
    const commentLen = cv.getUint16(p + 32, true)
    const localOffset = cv.getUint32(p + 42, true)
    const entryName = decoder.decode(cd.subarray(p + 46, p + 46 + nameLen))
    p += 46 + nameLen + extraLen + commentLen
    if (entryName !== name) continue

    if (compressed === 0xffffffff || size === 0xffffffff || localOffset === 0xffffffff) {
      throw new ZipFormatError('zip64 entry sizes are not supported for this entry')
    }
    if (size > maxBytes || compressed > maxBytes) throw new ZipFormatError(`${name} is too big`)

    // 3. Local header (its name/extra lengths can differ from the central copy), then the data.
    const local = view(await read(localOffset, localOffset + 29))
    if (local.getUint32(0, true) !== LOCAL_SIG) throw new ZipFormatError('bad local header')
    const dataStart = localOffset + 30 + local.getUint16(26, true) + local.getUint16(28, true)
    if (dataStart + compressed > zipSize) throw new ZipFormatError('entry out of range')
    const data = compressed === 0 ? new Uint8Array(0) : await read(dataStart, dataStart + compressed - 1)

    if (method === 0) return data
    if (method === 8) return await inflate(data, maxBytes)
    throw new ZipFormatError(`unsupported compression method ${method}`)
  }
  return null
}

function view(bytes: Uint8Array) {
  return new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength)
}

function u64(v: DataView, offset: number) {
  const value = v.getBigUint64(offset, true)
  if (value > BigInt(Number.MAX_SAFE_INTEGER)) throw new ZipFormatError('offset too large')
  return Number(value)
}

async function inflate(data: Uint8Array, maxBytes: number) {
  const stream = new Blob([data as BlobPart]).stream().pipeThrough(new DecompressionStream('deflate-raw'))
  const chunks: Uint8Array[] = []
  let total = 0
  for await (const chunk of stream as unknown as AsyncIterable<Uint8Array>) {
    total += chunk.length
    if (total > maxBytes) throw new ZipFormatError('entry inflates past the limit')
    chunks.push(chunk)
  }
  const out = new Uint8Array(total)
  let offset = 0
  for (const chunk of chunks) {
    out.set(chunk, offset)
    offset += chunk.length
  }
  return out
}
