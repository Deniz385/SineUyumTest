# syntax=docker/dockerfile:1

# --- BUILD AŞAMASI ---
# SDK imajında restore + publish yapılır; sonuç runtime imajına kopyalanır,
# böylece son imajda SDK ve ara dosyalar yer almaz.
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Önce yalnızca csproj'u kopyalayıp restore et — kaynak değişse de bağımlılık
# katmanı değişmediği sürece Docker bu adımı cache'ler.
COPY SineUyum.Api/SineUyum.Api.csproj SineUyum.Api/
RUN dotnet restore SineUyum.Api/SineUyum.Api.csproj

# Kalan kaynağı kopyala ve yayınla.
COPY SineUyum.Api/ SineUyum.Api/
RUN dotnet publish SineUyum.Api/SineUyum.Api.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# --- RUNTIME AŞAMASI ---
# Sadece ASP.NET Core çalışma zamanı; daha küçük ve daha az saldırı yüzeyi.
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Root olmayan kullanıcı yazabilsin diye veritabanı için ayrı bir dizin açılıp
# sahipliği ona verilir. Uygulama root olmadan çalıştığı için /app'e yazamaz;
# varsayılan bağlantı dizesi bu yazılabilir /data dizinini gösterir. Compose
# buraya kalıcı bir volume bağlar; PostgreSQL'e geçince (M2) bu kalkacak.
RUN mkdir -p /data/media && chown -R $APP_UID /data
ENV ConnectionStrings__DefaultConnection="Data Source=/data/sineuyum.db"
# Yüklenen profil fotoğrafları (B8). Compose buraya kalıcı volume bağlar.
ENV Storage__MediaRoot=/data/media

# Root olmayan kullanıcı (aspnet imajında hazır gelir) ile çalış.
USER $APP_UID

# Konteyner 8080'de dinler; ana makineye -p 5074:8080 ile bağlanır.
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

COPY --from=build /app/publish .

# Sağlık kontrolü imaja gömülmüyor: aspnet imajında curl/wget yok ve imajı
# şişirmemek için kurulmuyor. Uygulama /health ucunu sunuyor; orchestrator
# bununla problamalı — Kubernetes httpGet prob'u container içi araç
# gerektirmez, docker-compose örneği için README'ye bakın.

ENTRYPOINT ["dotnet", "SineUyum.Api.dll"]
