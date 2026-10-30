// PM2 konfiguratsiyasi: pm2 start ecosystem.config.cjs
module.exports = {
  apps: [
    {
      name: "pp-design",
      script: "node_modules/next/dist/bin/next",
      args: "start -p 3000",
      cwd: __dirname,
      env: { NODE_ENV: "production" },
      max_memory_restart: "700M",
    },
  ],
};
