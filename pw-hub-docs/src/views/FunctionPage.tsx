import { useParams } from 'react-router-dom'
import { allFunctionsIndex } from '../data/functions'

export function FunctionPage() {
  const { key } = useParams()
  const fn = allFunctionsIndex[key!]
  if (!fn) return <div>Функция не найдена</div>
  return (
    <div>
      <h2>{fn.name}</h2>
      <p>{fn.summary}</p>

      <section>
        <h3>Сигнатура</h3>
        <pre><code>{fn.signature}</code></pre>
      </section>

      {fn.params?.length ? (
        <section>
          <h3>Параметры</h3>
          <ul>
            {fn.params.map((p, i) => (
              <li key={i}><b>{p.name}</b>: {p.type} — {p.description}</li>
            ))}
          </ul>
        </section>
      ) : null}

      {fn.returns ? (
        <section>
          <h3>Возвращаемое значение</h3>
          <p>{fn.returns}</p>
        </section>
      ) : null}

      {fn.notes?.length ? (
        <section>
          <h3>Примечания</h3>
          <ul>
            {fn.notes.map((n, i) => (<li key={i}>{n}</li>))}
          </ul>
        </section>
      ) : null}

      {fn.examples?.length ? (
        <section>
          <h3>Примеры</h3>
          {fn.examples.map((ex, i) => (
            <div className="example" key={i}>
              <h4>{ex.title}</h4>
              {ex.description ? <p>{ex.description}</p> : null}
              <pre><code>{ex.code}</code></pre>
            </div>
          ))}
        </section>
      ) : null}
    </div>
  )
}
