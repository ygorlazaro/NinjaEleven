import React, { useEffect, useState } from 'react';
import { useNavigate, Link } from 'react-router-dom';
import { AuthApi, SeasonApi, CompetitionApi } from '@/api';
import { useAuthStore } from '@/state/auth';
import { divisionsOf } from '@/types';
import type { TeamDto, CompetitionEditionDto, SeasonDto } from '@/types';
import ClubCrest from '@/components/Club/ClubCrest';

const RegisterScreen: React.FC = () => {
  const navigate = useNavigate();
  const setAuth = useAuthStore((s) => s.setAuth);
  const setToken = useAuthStore((s) => s.setToken);

  // Step 1: account details
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [coachName, setCoachName] = useState('');

  // Step 2: club selection
  const [availableClubs, setAvailableClubs] = useState<TeamDto[]>([]);
  const [seasons, setSeasons] = useState<SeasonDto[]>([]);
  const [editions, setEditions] = useState<CompetitionEditionDto[]>([]);
  const [selectedSeasonId, setSelectedSeasonId] = useState<string>('');
  const [selectedEditionId, setSelectedEditionId] = useState<string>('');
  const [divisionClubs, setDivisionClubs] = useState<TeamDto[]>([]);
  const [selectedClubId, setSelectedClubId] = useState<string | null>(null);

  const [step, setStep] = useState<'account' | 'club'>('account');
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    if (step !== 'club') return;

    let cancelled = false;
    Promise.all([
      AuthApi.getAvailableClubs(),
      SeasonApi.list(),
    ])
      .then(([clubs, seasonsData]) => {
        if (cancelled) return;
        setAvailableClubs(clubs);
        setSeasons(seasonsData);

        const ordered = [...seasonsData].sort((a, b) => b.number - a.number);
        const current = ordered.find(s => s.status === 'InProgress') ?? ordered[0];
        if (!current) return;

        setSelectedSeasonId(current.id);
        return CompetitionApi.listEditionsBySeason(current.id);
      })
      .then(async (editionList) => {
        if (!editionList || cancelled) return;
        setEditions(editionList);

        const divisions = divisionsOf(editionList);
        const first = divisions[0] ?? editionList[0];
        if (first) setSelectedEditionId(first.id);
      })
      .catch(() => {
        if (!cancelled) setError('Não foi possível carregar os clubes disponíveis.');
      });

    return () => { cancelled = true; };
  }, [step]);

  useEffect(() => {
    if (!selectedEditionId) {
      setDivisionClubs([]);
      return;
    }

    const divisionIds = new Set(availableClubs.map(c => c.id));
    setDivisionClubs(availableClubs.filter(c => divisionIds.has(c.id)));
  }, [selectedEditionId, availableClubs]);

  const handleNext = () => {
    if (!email.trim() || !password || password.length < 6 || !coachName.trim()) {
      setError('Preencha todos os campos. A senha precisa de pelo menos 6 caracteres.');
      return;
    }
    setError(null);
    setStep('club');
  };

  const handleRegister = async () => {
    if (!selectedClubId) return;

    setLoading(true);
    setError(null);
    try {
      const res = await AuthApi.register({
        email,
        password,
        coachName: coachName.trim(),
        teamId: selectedClubId,
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

      {step === 'account' ? (
        <>
          <p>Crie sua conta e depois escolha um clube para comandar.</p>
          <form
            onSubmit={(e) => { e.preventDefault(); handleNext(); }}
            style={{ maxWidth: '320px', margin: '0 auto' }}
          >
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
              Próximo: escolher clube
            </button>
          </form>
          <div style={{ marginTop: '16px', fontSize: '13px' }}>
            <span style={{ color: 'var(--muted)' }}>Já tem uma conta? </span>
            <Link to="/login" style={{ color: 'var(--accent)' }}>Entrar</Link>
          </div>
        </>
      ) : (
        <>
          <p>Escolha um clube sem técnico para comandar como <strong>{coachName.trim()}</strong>.</p>

          {error && <div className="error">{error}</div>}

          {divisionClubs.map((club) => {
            const colors = { primary: club.primaryColor, secondary: club.secondaryColor };
            return (
              <div
                key={club.id}
                className={`team-choice ${selectedClubId === club.id ? 'selected' : ''}`}
                onClick={() => setSelectedClubId(club.id)}
                style={{ '--team-primary': colors.primary, '--team-secondary': colors.secondary } as React.CSSProperties}
              >
                <div className="team-choice__head">
                  <ClubCrest primary={colors.primary} secondary={colors.secondary} name={club.name} className="team-choice__crest" />
                  <div className="team-choice__identity">
                    <h3>{club.name}</h3>
                    {club.stadium && (
                      <div className="small">{club.stadium.name} • {club.stadium.capacity.toLocaleString('pt-BR')} lugares</div>
                    )}
                  </div>
                </div>
              </div>
            );
          })}

          {divisionClubs.length === 0 && !error && (
            <div className="small" style={{ marginTop: '12px' }}>Carregando clubes...</div>
          )}

          <div className="modal-actions" style={{ marginTop: '16px' }}>
            <button className="ctrl" onClick={() => setStep('account')}>Voltar</button>
            <button
              className="primary"
              disabled={!selectedClubId || loading}
              onClick={handleRegister}
            >
              {loading ? 'Criando...' : 'Confirmar e começar'}
            </button>
          </div>
        </>
      )}
    </div>
  );
};

export default RegisterScreen;
