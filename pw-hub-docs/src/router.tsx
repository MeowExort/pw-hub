import { createBrowserRouter } from 'react-router-dom'
import App from './App'
import { CategoryPage } from './views/CategoryPage'
import { FunctionPage } from './views/FunctionPage'
import { HomePage } from './views/HomePage'

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { index: true, element: <HomePage /> },
      { path: 'category/:key', element: <CategoryPage /> },
      { path: 'function/:key', element: <FunctionPage /> },
    ],
  },
])
