import { useAuth } from '../context/AuthContext';
import { useSignalR } from '../context/SignalRContext';
import { useEffect, useState } from 'react';

export default function Home() {
  const { user, logout } = useAuth();
  const connection = useSignalR();
  const [hubStatus, setHubStatus] = useState('connecting');

  useEffect(() => {
    if (connection) setHubStatus('connected');
  }, [connection]);

  return (
    <div>
      <h1>Welcome, {user.fullName}</h1>
      <p>Role: {user.role}</p>
      <p>SignalR: {hubStatus}</p>
      <button onClick={logout}>Log out</button>
    </div>
  );
}