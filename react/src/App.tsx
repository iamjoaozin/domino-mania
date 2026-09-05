import React, { useState, useEffect } from 'react';
import './index.css';

// Ignora checagem estrita para o objeto global do Unity
declare const Unity: any;

const REWARDS = [50, 100, 200, 500, 1000, 2000];

export default function App() {
  const [canSpin, setCanSpin] = useState(false);
  const [timeLeft, setTimeLeft] = useState(0);
  const [spinning, setSpinning] = useState(false);
  const [currentDisplay, setCurrentDisplay] = useState<number | string>("?");
  const [result, setResult] = useState<number | null>(null);

  useEffect(() => {
    // Ao iniciar, puxa a API do C#
    if (typeof Unity !== 'undefined' && Unity.Globals && Unity.Globals.RouletteAPI) {
      const api = Unity.Globals.RouletteAPI;
      const isReady = api.CanSpin();
      setCanSpin(isReady);
      if (!isReady) {
        setTimeLeft(api.GetTimeRemaining());
      }
    } else {
      // Mock para testar no navegador (npm start)
      setCanSpin(true);
    }
  }, []);

  // Countdown lógico para o React
  useEffect(() => {
    let interval: any;
    if (!canSpin && timeLeft > 0) {
      interval = setInterval(() => {
        setTimeLeft(prev => {
          if (prev <= 1) {
            setCanSpin(true);
            return 0;
          }
          return prev - 1;
        });
      }, 1000);
    }
    return () => clearInterval(interval);
  }, [canSpin, timeLeft]);

  const formatTime = (seconds: number) => {
    const h = Math.floor(seconds / 3600).toString().padStart(2, '0');
    const m = Math.floor((seconds % 3600) / 60).toString().padStart(2, '0');
    const s = Math.floor(seconds % 60).toString().padStart(2, '0');
    return `${h}:${m}:${s}`;
  };

  const playClick = () => {
    if (typeof Unity !== 'undefined' && Unity.Globals && Unity.Globals.PlayClickSound) {
      Unity.Globals.PlayClickSound();
    }
  };

  const spin = () => {
    if (!canSpin || spinning) return;
    playClick();
    setSpinning(true);
    setResult(null);
    setCanSpin(false);

    let finalIndex = 0;
    
    // Comunicação com o Backend C#
    if (typeof Unity !== 'undefined' && Unity.Globals && Unity.Globals.RouletteAPI) {
      finalIndex = Unity.Globals.RouletteAPI.Spin();
      // O C# retorna o índice do array, e registra o timestamp
      // Setamos 24h mock no React para começar o countdown localmente
      setTimeLeft(24 * 3600);
    } else {
      // Mock local
      finalIndex = Math.floor(Math.random() * REWARDS.length);
      setTimeLeft(24 * 3600);
    }

    const finalPrize = REWARDS[finalIndex];

    // Animação Digital "Decipher" (Sorteio Hacker)
    let cycles = 0;
    const maxCycles = 30; // 3 segundos se for a cada 100ms
    const interval = setInterval(() => {
      cycles++;
      playClick(); // Clicar rapido durante a roleta
      const randomPrize = REWARDS[Math.floor(Math.random() * REWARDS.length)];
      setCurrentDisplay(randomPrize);

      if (cycles >= maxCycles) {
        clearInterval(interval);
        setSpinning(false);
        setCurrentDisplay(finalPrize);
        setResult(finalPrize);
        
        // Dá o prêmio no C#
        if (typeof Unity !== 'undefined' && Unity.Globals && Unity.Globals.RouletteAPI) {
          Unity.Globals.RouletteAPI.ClaimReward(finalPrize);
        }
      }
    }, 100); // Rápido efeito digital
  };

  const isLocked = !canSpin && !spinning;

  return (
    <div className="flex flex-col items-center justify-center w-full h-full bg-slate-950 text-white font-sans bg-[url('https://www.transparenttextures.com/patterns/stardust.png')]">
      {/* Efeito de Luz de Fundo */}
      <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-96 h-96 bg-purple-600/20 rounded-full blur-[100px]"></div>

      <div className="relative bg-slate-900/60 backdrop-blur-xl p-10 rounded-3xl shadow-2xl border border-white/10 max-w-lg w-full flex flex-col items-center">
        <h1 className="text-4xl font-extrabold mb-2 text-transparent bg-clip-text bg-gradient-to-r from-cyan-400 via-purple-500 to-pink-500 drop-shadow-[0_0_10px_rgba(168,85,247,0.8)]">
          Cyber Spin
        </h1>
        <p className="text-slate-300 mb-10 text-center font-medium">
          {isLocked ? "Sua energia recarrega em:" : "Sorteie sua recompensa diária!"}
        </p>

        {/* Digital Orb Spinner */}
        <div className={`relative w-64 h-64 rounded-full flex items-center justify-center mb-10 border-4 transition-all duration-300 ${spinning ? 'border-cyan-400 shadow-[0_0_50px_rgba(34,211,238,0.6)] animate-pulse' : isLocked ? 'border-slate-700 shadow-none' : 'border-purple-500 shadow-[0_0_30px_rgba(168,85,247,0.4)] hover:shadow-[0_0_40px_rgba(168,85,247,0.6)]'}`}>
          <div className="absolute inset-2 rounded-full border border-white/5 bg-slate-950/80 backdrop-blur-md flex flex-col items-center justify-center">
            {isLocked ? (
              <>
                <span className="text-sm text-slate-400 mb-2 uppercase tracking-widest">Cooldown</span>
                <span className="text-3xl font-mono text-cyan-400 drop-shadow-[0_0_8px_rgba(34,211,238,0.8)]">{formatTime(timeLeft)}</span>
              </>
            ) : (
              <>
                <span className="text-5xl font-black text-transparent bg-clip-text bg-gradient-to-b from-white to-slate-400 drop-shadow-[0_0_10px_rgba(255,255,255,0.5)]">
                  {currentDisplay}
                </span>
                {typeof currentDisplay === 'number' && <span className="text-sm text-purple-400 mt-2 uppercase font-bold tracking-widest">Moedas</span>}
              </>
            )}
          </div>
        </div>

        {/* Reward Highlight */}
        <div className={`h-16 mb-4 flex items-center justify-center transition-all duration-500 ${result ? 'opacity-100 scale-100' : 'opacity-0 scale-90'}`}>
          {result && (
            <div className="px-6 py-3 bg-gradient-to-r from-green-500/20 to-emerald-500/20 border border-green-400/50 rounded-xl text-green-300 font-bold text-xl drop-shadow-[0_0_10px_rgba(74,222,128,0.5)] animate-bounce">
              + {result} Moedas Adicionadas!
            </div>
          )}
        </div>

        {/* Action Button */}
        <button 
          onClick={spin}
          disabled={isLocked || spinning}
          className={`w-full py-4 rounded-xl font-black text-xl tracking-wider uppercase transition-all duration-300 ${isLocked || spinning ? 'bg-slate-800 text-slate-500 border border-slate-700 cursor-not-allowed' : 'bg-gradient-to-r from-cyan-500 to-purple-600 hover:from-cyan-400 hover:to-purple-500 text-white shadow-[0_0_20px_rgba(168,85,247,0.5)] hover:shadow-[0_0_30px_rgba(34,211,238,0.6)] active:scale-95 border border-white/20'}`}
        >
          {spinning ? 'Decodificando...' : isLocked ? 'Bloqueado' : 'Girar Agora'}
        </button>
      </div>
    </div>
  );
}
