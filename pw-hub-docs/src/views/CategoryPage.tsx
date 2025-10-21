import { Link, useParams } from 'react-router-dom'
import { categories } from '../data/functions'

export function CategoryPage() {
  const { key } = useParams()
  const cat = categories.find(c => c.key === key)
  if (!cat) return <div>Раздел не найден</div>
  return (
    <div>
      <h2>{cat.title}</h2>
      <p>{cat.description}</p>
      <h3>Функции</h3>
      <ul>
        {cat.functions.map(fn => (
          <li key={fn.key}>
            <Link to={`/function/${fn.key}`}>{fn.name}</Link> — {fn.summary}
          </li>
        ))}
      </ul>
      {cat.examples?.length ? (
        <>
          <h3>Комбинированные примеры</h3>
          {cat.examples.map((ex, i) => (
            <div className="example" key={i}>
              <h4>{ex.title}</h4>
              <p>{ex.description}</p>
              <pre><code>{ex.code}</code></pre>
            </div>
          ))}
        </>
      ) : null}
    </div>
  )
}
