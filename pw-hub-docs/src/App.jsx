import { Routes, Route } from 'react-router-dom'
import Header from './components/Header'
import Sidebar from './components/Sidebar'
import ScrollToTop from './components/ScrollToTop'
import ScrollToTopButton from './components/ScrollToTopButton'
import Home from './pages/Home'
import AccountApi from './pages/AccountApi'
import BrowserApi from './pages/BrowserApi'
import Utilities from './pages/Utilities'

function App() {
    return (
        <div className="app">
            <ScrollToTop />
            <ScrollToTopButton />
            <Header />
            <div className="main-container">
                <Sidebar />
                <main className="content">
                    <Routes>
                        <Route path="/" element={<Home />} />
                        <Route path="/account" element={<AccountApi />} />
                        <Route path="/browser" element={<BrowserApi />} />
                        <Route path="/utilities" element={<Utilities />} />
                    </Routes>
                </main>
            </div>
        </div>
    )
}

export default App