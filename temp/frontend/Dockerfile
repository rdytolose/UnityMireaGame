FROM nginx:1.27-alpine

# Static site.
COPY index.html pair.html shop.html clues.html chat.html wait.html admin.html story.html over.html \
     app.js config.js styles.css /usr/share/nginx/html/

# Nginx config that serves the site and proxies /api -> backend.
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80
