import { createContext, useContext, useEffect, useRef, useState } from 'react';
import * as signalR from '@microsoft/signalr';
import { useAuth } from './AuthContext';

const SignalRContext = createContext(null);

export function SignalRProvider({ children }) {
  const { user } = useAuth();
  const connectionRef = useRef(null);
  const [connection, setConnection] = useState(null);

  useEffect(() => {
    if (!user) {
      connectionRef.current?.stop();
      connectionRef.current = null;
      setConnection(null);
      return;
    }

    const conn = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/app')
      .withAutomaticReconnect()
      .build();

    conn.start()
      .then(() => setConnection(conn))
      .catch(console.error);

    connectionRef.current = conn;

    return () => { conn.stop(); };
  }, [user]);

  return (
    <SignalRContext.Provider value={connection}>
      {children}
    </SignalRContext.Provider>
  );
}

export function useSignalR() {
  return useContext(SignalRContext);
}