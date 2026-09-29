import { RankingApi } from '@/api';
import ClubCrest from '@/components/Club/ClubCrest';
import type { ClubRankingDto } from '@/types';
import React, { useEffect, useState } from 'react';

const RankingScreen: React.FC = () => {
  const [ranking, setRanking] = useState<ClubRankingDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const loadRanking = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await RankingApi.getRanking();
        setRanking(data);
      } catch (err: any) {
        setError(err.message || 'Não foi possível carregar o ranking.');
      } finally {
        setLoading(false);
      }
    };

    loadRanking();
  }, []);

  if (loading) {
    return (
      <div className="card start">
        <p>Carregando ranking...</p>
      </div>
    );
  }

  if (error) {
    return (
      <div className="card start">
        <h1>Erro</h1>
        <p>{error}</p>
      </div>
    );
  }

  const getDivisionName = (division: number): string => {
    switch (division) {
      case 1: return '1ª Divisão';
      case 2: return '2ª Divisão';
      case 3: return '3ª Divisão';
      case 4: return '4ª Divisão';
      default: return '—';
    }
  };

  return (
    <div className="card">
      <h1>🏆 Ranking Ninja</h1>
      <p className="small" style={{ marginBottom: '16px', color: 'var(--muted)' }}>
        O ranking combina desempenho no campeonato (últimas 3 temporadas) e na copa (últimas 3 temporadas).
      </p>

      <div style={{ overflowX: 'auto' }}>
        <table className="data-table">
          <thead>
            <tr>
              <th style={{ width: '50px' }}>Pos</th>
              <th>Clube</th>
              <th style={{ width: '120px', textAlign: 'center' }}>Divisão</th>
              <th style={{ width: '100px', textAlign: 'right' }}>Força</th>
              <th style={{ width: '120px', textAlign: 'right' }}>Pontos Base</th>
              <th style={{ width: '100px', textAlign: 'right' }}>Pontos Copa</th>
              <th style={{ width: '120px', textAlign: 'right' }}>Total</th>
            </tr>
          </thead>
          <tbody>
            {ranking.map((club) => (
              <tr key={club.teamId}>
                <td style={{ fontWeight: 'bold', color: club.position <= 3 ? 'var(--accent)' : 'var(--text)' }}>
                  {club.position}º
                </td>
                <td>
                  <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                    <ClubCrest
                      primary={club.primaryColor}
                      secondary={club.secondaryColor}
                      name={club.teamName}
                      className="team-choice__crest"
                      style={{ width: '28px', height: '28px' }}
                    />
                    <div>
                      <span style={{ fontWeight: 500 }}>{club.teamName}</span>
                      <div className="small" style={{ color: 'var(--muted)' }}>
                        {club.managerName}
                      </div>
                    </div>
                  </div>
                </td>
                <td style={{ textAlign: 'center' }}>
                  <span className="badge" style={{
                    backgroundColor: club.currentDivision === 1 ? 'var(--accent)' :
                                   club.currentDivision === 2 ? '#3a6ea5' :
                                   club.currentDivision === 3 ? '#2a5a8a' : '#1a4a6e'
                  }}>
                    {getDivisionName(club.currentDivision)}
                  </span>
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                  {club.strength.toFixed(1)}
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                  {club.rankingBase}
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace' }}>
                  {club.cupScore}
                </td>
                <td style={{ textAlign: 'right', fontFamily: 'monospace', fontWeight: 'bold', color: 'var(--accent)' }}>
                  {club.rankingPoints}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {ranking.length === 0 && (
        <div className="card start">
          <p>Nenhum clube no ranking.</p>
        </div>
      )}
    </div>
  );
};

export default RankingScreen;
