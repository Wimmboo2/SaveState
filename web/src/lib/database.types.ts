// Generated from the Supabase schema (supabase gen types), trimmed to what the app uses.
// Regenerate after schema changes.
export type Json = string | number | boolean | null | { [key: string]: Json | undefined } | Json[]

export type Database = {
  __InternalSupabase: {
    PostgrestVersion: '14.5'
  }
  public: {
    Tables: {
      backups: {
        Row: {
          apps: Json
          expires_at: string | null
          file_path: string | null
          files: Json | null
          files_removed_at: string | null
          files_removed_reason: string | null
          id: string
          size_bytes: number
          updated_at: string
          uploaded_at: string | null
          user_id: string
        }
        Insert: {
          apps?: Json
          expires_at?: string | null
          file_path?: string | null
          files?: Json | null
          files_removed_at?: string | null
          files_removed_reason?: string | null
          id?: string
          size_bytes?: number
          updated_at?: string
          uploaded_at?: string | null
          user_id?: string
        }
        Update: {
          apps?: Json
          expires_at?: string | null
          file_path?: string | null
          files?: Json | null
          files_removed_at?: string | null
          files_removed_reason?: string | null
          id?: string
          size_bytes?: number
          updated_at?: string
          uploaded_at?: string | null
          user_id?: string
        }
        Relationships: []
      }
    }
    Views: { [_ in never]: never }
    Functions: {
      keepalive: { Args: never; Returns: string }
    }
    Enums: { [_ in never]: never }
    CompositeTypes: { [_ in never]: never }
  }
}
