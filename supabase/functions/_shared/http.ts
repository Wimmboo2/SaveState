export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' },
  })
}

/** Error shape understood by the desktop app and website: a friendly sentence plus a machine code. */
export function fail(status: number, code: string, error: string): Response {
  return json({ error, code }, status)
}
