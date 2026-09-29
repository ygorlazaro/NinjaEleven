import React, { useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AuthApi } from '@/api';
import { useAuthStore } from '@/state/auth';

const RegisterScreen: React.FC = () => {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [coachName, setCoachName] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const handleRegister = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!email.trim() || !password || password.length < 6 || !coachName.trim()) {
      setError('Preencha todos os campos. A senha precisa de pelo menos 6 caracteres.');
      return;
    }

    setLoading(true);
    setError(null);
    try {
      const res = await AuthApi.register({
        email,
        password,
        coachName: coachName.trim(),
      });
      setAuth(res.token, res.userId, res.email, res.teamId ?? null, res.coachName ?? null);

      if (res.teamId) {
        navigate(`/team/${res.teamId}`);
      }
    } catch (err: any) {
      setError(err.message || 'Não foi possível criar a conta.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="card start">
      <h1>Criar conta</h1>
      <p>Crie sua conta e um clube será atribuído automaticamente.</p>

      <form onSubmit={handleRegister} style={{ maxWidth: '320px', margin: '0 auto' }}>
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
        <div style={{ marginBottom: '12px' }}>
          <input
            type="password"
            className="ctrl"
            placeholder="Senha (mínimo 6 caracteres)"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
            minLength={6}
            disabled={loading}
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        <div style={{ marginBottom: '16px' }}>
          <input
            type="text"
            className="ctrl"
            placeholder="Nome do técnico"
            value={coachName}
            onChange={(e) => setCoachName(e.target.value)}
            required
            disabled={loading}
            style={{ width: '100%', padding: '8px' }}
          />
        </div>
        {error && <div className="error">{error}</div>}
        <button type="submit" className="primary" disabled={loading} style={{ width: '100%' }}>
          {loading ? 'Criando...' : 'Criar conta e começar'}
        </button>
      </form>

      <div style={{ marginTop: '16px', fontSize: '13px' }}>
        <span style={{ color: 'var(--muted)' }}>Já tem uma conta? </span>
        <Link to="/login" style={{ color: 'var(--accent)' }}>Entrar</Link>
      </div>
    </div>
  );
};

export default RegisterScreen;