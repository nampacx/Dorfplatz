import { api } from '../../api/client'
import { ErrorNotice, Loading, RequireRole, useAsync } from '../../components/Common'
import type { AdminUser } from '../../types'

function UserTable() {
  const { data, loading, error, reload } = useAsync(() => api.get<AdminUser[]>('/admin/users'), [])

  async function toggle(u: AdminUser, role: 'moderator' | 'admin', grant: boolean) {
    await api.post(`/admin/users/${u.id}/roles`, { role, grant })
    reload()
  }

  if (loading) return <Loading />
  if (error) return <ErrorNotice message={error} />

  return (
    <div className="panel">
      <table>
        <thead>
          <tr><th>Mitglied</th><th>Rollen</th><th>Aktionen</th></tr>
        </thead>
        <tbody>
          {data?.map((u) => {
            const isMod = u.roles.includes('moderator')
            const isAdmin = u.roles.includes('admin')
            return (
              <tr key={u.id}>
                <td>
                  <div style={{ fontWeight: 600 }}>{u.displayName ?? '—'}</div>
                  <div style={{ color: 'var(--ink-soft)', fontSize: '0.82rem' }}>{u.email}</div>
                </td>
                <td>{u.roles.map((r) => <span key={r} className="badge role" style={{ marginRight: 4 }}>{r}</span>)}</td>
                <td>
                  <button className="btn small secondary" onClick={() => toggle(u, 'moderator', !isMod)}>
                    {isMod ? 'Moderator entziehen' : 'Zum Moderator machen'}
                  </button>{' '}
                  <button className="btn small secondary" onClick={() => toggle(u, 'admin', !isAdmin)}>
                    {isAdmin ? 'Admin entziehen' : 'Zum Admin machen'}
                  </button>
                </td>
              </tr>
            )
          })}
        </tbody>
      </table>
    </div>
  )
}

export default function AdminPage() {
  return (
    <RequireRole role="admin">
      <div className="page-head">
        <h1>Administration</h1>
        <p>Mitglieder verwalten und Moderatoren ernennen.</p>
      </div>
      <UserTable />
    </RequireRole>
  )
}
