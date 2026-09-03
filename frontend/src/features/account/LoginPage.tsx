import { Link, Navigate, useSearchParams } from 'react-router-dom'
import { useAuth } from '../../auth/useAuth'
import ProviderButtons from '../../auth/ProviderButtons'
import { Loading } from '../../components/Common'

/**
 * Single entry point for signing in. Every path that needs an identity — the
 * header button, a protected route, an expired session — lands here so the
 * visitor picks a provider instead of being sent to one.
 */
export default function LoginPage() {
  const { me, loading } = useAuth()
  const [params] = useSearchParams()
  const redirectTo = params.get('redirect') ?? '/'

  if (loading) return <Loading />
  if (me) return <Navigate to={redirectTo} replace />

  return (
    <div className="login">
      <p className="u-label">Anmeldung</p>
      <h1>Willkommen auf dem Dorfplatz</h1>
      <p className="login-lead">
        Melde dich an, um Events einzureichen, Anzeigen aufzugeben und anderen
        Mitgliedern zu schreiben. Ein bestehendes Microsoft- oder Google-Konto genügt —
        du musst kein neues Passwort anlegen.
      </p>

      <ProviderButtons redirectTo={redirectTo} />

      <p className="login-note">
        Wir speichern nur deinen Namen und deine E-Mail-Adresse. Details in der{' '}
        <Link to="/privacy">Datenschutzerklärung</Link>.
      </p>
    </div>
  )
}
