import { Link, NavLink, Outlet } from 'react-router-dom'
import { categories } from './data/functions'

export default function App() {
  return (
    <div className="app">
      <header className="header">
        <div className="container">
          <h1 className="logo"><Link to="/">PW Hub — Lua API</Link></h1>
          <nav className="topnav">
            <a href="https://vitejs.dev" target="_blank" rel="noreferrer">Vite</a>
            <a href="https://react.dev" target="_blank" rel="noreferrer">React</a>
          </nav>
        </div>
      </header>
      <div className="container layout">
        <aside className="sidebar">
          <div className="sidebar-section">
            <h3>Разделы</h3>
            <ul>
              {categories.map(c => (
                <li key={c.key}>
                  <NavLink to={`/category/${c.key}`}>{c.title}</NavLink>
                </li>
              ))}
            </ul>
          </div>
          <div className="sidebar-section">
            <h3>Все функции</h3>
            <ul>
              {categories.flatMap(c => c.functions).map(fn => (
                <li key={fn.key}>
                  <NavLink to={`/function/${fn.key}`}>{fn.name}</NavLink>
                </li>
              ))}
            </ul>
          </div>
        </aside>
        <main className="content">
          <Outlet />
        </main>
      </div>
      <footer className="footer">
        <div className="container">Документация генерируется вручную на основе LuaIntegration.cs. Актуально на 2025-10-21.</div>
      </footer>
    </div>
  )
}
