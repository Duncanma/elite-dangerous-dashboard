import { NavLink, Route, Routes } from "react-router-dom";
import { useDashboardState } from "./useDashboardState";
import DashboardPage from "./pages/DashboardPage";
import EconomicsPage from "./pages/EconomicsPage";
import FleetPage from "./pages/FleetPage";
import TradePage from "./pages/TradePage";
import SystemsPage from "./pages/SystemsPage";
import PowerplayPage from "./pages/PowerplayPage";
import SettingsPage from "./pages/SettingsPage";
import "./App.css";

export default function App() {
  const { connected } = useDashboardState();

  return (
    <div className="dashboard">
      <header className="dashboard-header">
        <div className="dashboard-header-title">
          <img src="/branding/elite-dangerous-wordmark.png" alt="" className="header-logo" />
          <h1>Elite Dangerous Dashboard</h1>
        </div>
        <span className={`connection-badge ${connected ? "connected" : "disconnected"}`}>
          {connected ? "Live" : "Connecting…"}
        </span>
      </header>

      <nav className="tabs">
        <NavLink to="/" end className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Dashboard
        </NavLink>
        <NavLink to="/economics" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Economics
        </NavLink>
        <NavLink to="/fleet" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Fleet
        </NavLink>
        <NavLink to="/trade" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Trade
        </NavLink>
        <NavLink to="/systems" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          My Systems
        </NavLink>
        <NavLink to="/powerplay" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Powerplay
        </NavLink>
        <NavLink to="/settings" className={({ isActive }) => (isActive ? "tab active" : "tab")}>
          Settings
        </NavLink>
      </nav>

      <Routes>
        <Route path="/" element={<DashboardPage />} />
        <Route path="/economics" element={<EconomicsPage />} />
        <Route path="/fleet" element={<FleetPage />} />
        <Route path="/trade" element={<TradePage />} />
        <Route path="/systems" element={<SystemsPage />} />
        <Route path="/powerplay" element={<PowerplayPage />} />
        <Route path="/settings" element={<SettingsPage />} />
      </Routes>
    </div>
  );
}
