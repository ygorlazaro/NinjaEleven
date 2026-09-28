import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AuthApi, TeamApi } from '@/api';
import { useAuthStore } from '@/state/auth';
import { useGameState } from '@/state';
import type { TeamDto } from '@/types';

const LoginScreen: React.FC = () => {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const setSelectedTeam = useGameState((s) => s.setSelectedTeam);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password) return;

    setLoading(true);
    setError(null);
    try {
      const res = await AuthApi.login({ email, password });
      setAuth(res.token, res.userId, res.email, res.teamId ?? null, res.coachName ?? null);

      if (res.teamId) {
        try {
          const team = await TeamApi.get(res.teamId);
          setSelectedTeam(team);
        } catch {
          // Team may have been deleted; AuthRoot will clear auth on the next render.
        }
      }

      if (res.teamId) {
        navigate(`/team/${res.teamId}`);
      } else {
        navigate('/');
      }
    } catch (err: any) {
      setError(err.message || 'Não foi possível entrar.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card start">
      <h1>Entrar</h1>
      <p>Digite seu e-mail e senha para retomar a sua carreira.</p>

      <form onSubmit={handleLogin} style={{ maxWidth: '320px', margin: '0 auto' }}>
        <div style={{ marginBottom: '12px' }}>
          <input
            type="email"
            className="ctrl"
            placeholder="E-mail"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            required
            disabled={loading}
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        <div style={{ marginBottom: '16px' }}>
          <input
            type="password"
            className="ctrl"
            placeholder="Senha"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            disabled={loading}
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        {error && <div className="error">{error}</div>}
        <button type="submit" className="primary" disabled={loading} style={{ width: '100%' }}>
          {loading ? 'Entrando...' : 'Entrar'}
        </button>
      </form>

      <div style={{ marginTop: '16px', fontSize: '13px' }}>
        <span style={{ color: 'var(--muted)' }}>Ainda não tem uma conta? </span>
        <Link to="/register" style={{ color: 'var(--accent)' }}>Criar conta</Link>
      </div>
    </div>
  );
};

export default LoginScreen;
