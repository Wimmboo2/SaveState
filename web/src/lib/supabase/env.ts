// Only public values live here: the project URL and the publishable (anon) key.
// Row Level Security is what protects the data. Never put a secret/service key in NEXT_PUBLIC_*.
export const supabaseUrl = process.env.NEXT_PUBLIC_SUPABASE_URL!
export const supabaseAnonKey = process.env.NEXT_PUBLIC_SUPABASE_ANON_KEY!
