# Instala LibMan CLI globalmente (solo una vez)
dotnet tool install -g Microsoft.Web.LibraryManager.Cli

# Restaura las librerías
libman restore


# Entrar al contenedor del BFF
docker exec -it ecommerce-grpc-apigateway-bff /bin/bash

# Probar resolución DNS y conectividad
ping ecommerce-grpc-productservice
curl http://ecommerce-grpc-productservice:7001

grpcurl -plaintext -d @ localhost:7001 productservice.ProductService/GetProduct < request.json

# Verificar ProductService
grpcurl -plaintext localhost:7001 list

# Probar un endpoint específico
grpcurl -plaintext -d '{"id": 1}' localhost:7001 productservice.ProductService/GetProduct

--troubleshooting
# Entrar al contenedor
docker exec -it ecommerce-grpc-apigateway-bff /bin/bash

# Actualizar el índice de paquetes
apt-get update

# Instalar herramientas de red
apt-get install -y iputils-ping curl dnsutils

# Ahora puede usar ping y curl
ping ecommerce-grpc-productservice
curl http://ecommerce-grpc-productservice:7001