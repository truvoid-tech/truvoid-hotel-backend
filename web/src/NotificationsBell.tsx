import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from './api'

type Notice = { id: string; kind: string; title: string; body: string | null; link: string | null; createdAt: string; readAt: string | null }
type Feed = { unread: number; items: Notice[] }

function BellIcon() {
  return <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2"
    strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M18 8A6 6 0 0 0 6 8c0 7-3 9-3 9h18s-3-2-3-9" />
    <path d="M13.73 21a2 2 0 0 1-3.46 0" />
  </svg>
}

/** In-app notifications: unread count, a dropdown, and mark-as-read. */
export function NotificationsBell() {
  const [feed, setFeed] = useState<Feed>({ unread: 0, items: [] })
  const [open, setOpen] = useState(false)

  async function load() {
    try { setFeed(await api.get<Feed>('/v1/notifications?limit=12')) } catch { /* keep the last feed */ }
  }
  useEffect(() => {
    void load()
    const id = window.setInterval(load, 60_000)
    return () => window.clearInterval(id)
  }, [])

  async function markAll() {
    try { await api.post('/v1/notifications/read-all', {}) } catch { /* ignore */ }
    void load()
  }
  async function openNotice(notice: Notice) {
    if (!notice.readAt) {
      try { await api.post(`/v1/notifications/${notice.id}/read`, {}) } catch { /* ignore */ }
      void load()
    }
    setOpen(false)
  }

  return <div className="notif">
    <button type="button" className="notif-bell" aria-expanded={open}
      aria-label={`Notifications${feed.unread ? `, ${feed.unread} unread` : ''}`} onClick={() => setOpen((value) => !value)}>
      <BellIcon />
      {feed.unread > 0 && <span className="notif-badge">{feed.unread > 9 ? '9+' : feed.unread}</span>}
    </button>
    {open && <div className="notif-panel" role="dialog" aria-label="Notifications">
      <div className="notif-head">
        <strong>Notifications</strong>
        {feed.unread > 0 && <button className="link-button" onClick={() => void markAll()}>Mark all read</button>}
        <button className="link-button" onClick={() => setOpen(false)}>Close</button>
      </div>
      {feed.items.length ? <ul className="notif-list">
        {feed.items.map((notice) => <li key={notice.id} className={notice.readAt ? 'read' : ''}>
          {notice.link
            ? <Link to={notice.link} onClick={() => void openNotice(notice)}>
                <strong>{notice.title}</strong>
                {notice.body && <span>{notice.body}</span>}
                <small>{new Date(notice.createdAt).toLocaleString()}</small>
              </Link>
            : <div className="notif-static">
                <strong>{notice.title}</strong>
                {notice.body && <span>{notice.body}</span>}
                <small>{new Date(notice.createdAt).toLocaleString()}</small>
              </div>}
        </li>)}
      </ul> : <div className="empty">You're all caught up.</div>}
    </div>}
  </div>
}
