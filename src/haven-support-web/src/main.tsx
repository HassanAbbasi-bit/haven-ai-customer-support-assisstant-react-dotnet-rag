import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { SupportApp } from './SupportApp';
import './styles.css';
import './support.css';
const root = document.getElementById('root');
if (!root) throw new Error('Application root element is missing.');
createRoot(root).render(
  <StrictMode>
    <SupportApp />
  </StrictMode>,
);
