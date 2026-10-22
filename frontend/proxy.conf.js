// Proxy du serveur de développement : /api -> API .NET.
// Dans Docker, API_URL vaut http://api:8080 (voir docker-compose.yml).
module.exports = {
  '/api': {
    target: process.env.API_URL || 'http://localhost:5080',
    secure: false,
    changeOrigin: true,
  },
};
