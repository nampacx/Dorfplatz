import { useLocation } from 'react-router-dom'

/**
 * The identity providers the board accepts. Static Web Apps handles the flow;
 * these links just enter it and come back to where the visitor started.
 */
export default function ProviderButtons({ redirectTo }: { redirectTo?: string }) {
  const location = useLocation()
  const target = encodeURIComponent(redirectTo ?? location.pathname + location.search)

  return (
    <div className="providers">
      <a className="btn provider" href={`/.auth/login/aad?post_login_redirect_uri=${target}`}>
        Weiter mit Microsoft
      </a>
      <a className="btn secondary provider" href={`/.auth/login/google?post_login_redirect_uri=${target}`}>
        Weiter mit Google
      </a>
    </div>
  )
}
