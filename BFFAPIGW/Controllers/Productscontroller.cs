using ECommerceGRPC.ProductService;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BFFAPIGW.Controllers
{

    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación JWT para todos los endpoints
    public class ProductsController : ControllerBase
    {
        private readonly ProductService.ProductServiceClient _productClient;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ProductService.ProductServiceClient productClient,
            ILogger<ProductsController> logger)
        {
            _productClient = productClient;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los productos con paginación
        /// </summary>
        [HttpGet]
        [AllowAnonymous] // Este endpoint es público
        [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? category = null,
            [FromQuery] double? minPrice = null,
            [FromQuery] double? maxPrice = null)
        {
            try
            {
                _logger.LogInformation("Obteniendo productos - Página: {Page}, Tamaño: {Size}",
                    pageNumber, pageSize);

                var request = new GetProductsRequest
                {
                    PageNumber = pageNumber,
                    PageSize = pageSize,
                    Category = category ?? string.Empty,
                    MinPrice = minPrice ?? 0,
                    MaxPrice = maxPrice ?? 0
                };

                var products = new List<ProductDto>();
                var streamingCall = _productClient.GetProducts(request);

                await foreach (var product in streamingCall.ResponseStream.ReadAllAsync())
                {
                    products.Add(MapToProductDto(product));
                }

                _logger.LogInformation("Se obtuvieron {Count} productos", products.Count);

                return Ok(new
                {
                    success = true,
                    data = products,
                    pageNumber,
                    pageSize,
                    totalCount = products.Count
                });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al obtener productos");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener productos");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener un producto por ID
        /// </summary>
        [HttpGet("{id}")]
        [AllowAnonymous] // Este endpoint es público
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProduct(int id)
        {
            try
            {
                _logger.LogInformation("Obteniendo producto con ID: {ProductId}", id);

                var request = new GetProductRequest { Id = id };
                var product = await _productClient.GetProductAsync(request);

                return Ok(new
                {
                    success = true,
                    data = MapToProductDto(product)
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Producto no encontrado: {ProductId}", id);
                return NotFound(new { success = false, message = $"Producto con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al obtener producto");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener producto");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Crear un nuevo producto (solo Admin)
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateProduct([FromBody] CreateProductDto dto)
        {
            try
            {
                _logger.LogInformation("Creando nuevo producto: {Name}", dto.Name);

                var request = new CreateProductRequest
                {
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    Stock = dto.Stock,
                    Category = dto.Category
                };

                var product = await _productClient.CreateProductAsync(request);

                _logger.LogInformation("Producto creado exitosamente con ID: {ProductId}", product.Id);

                return CreatedAtAction(
                    nameof(GetProduct),
                    new { id = product.Id },
                    new
                    {
                        success = true,
                        message = "Producto creado exitosamente",
                        data = MapToProductDto(product)
                    });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                _logger.LogWarning("Datos de producto inválidos: {Details}", ex.Status.Detail);
                return BadRequest(new { success = false, message = ex.Status.Detail });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al crear producto");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear producto");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualizar un producto existente (solo Admin)
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateProduct(int id, [FromBody] UpdateProductDto dto)
        {
            try
            {
                _logger.LogInformation("Actualizando producto con ID: {ProductId}", id);

                var request = new UpdateProductRequest
                {
                    Id = id,
                    Name = dto.Name,
                    Description = dto.Description,
                    Price = dto.Price,
                    Stock = dto.Stock,
                    Category = dto.Category,
                    IsActive = dto.IsActive
                };

                var product = await _productClient.UpdateProductAsync(request);

                _logger.LogInformation("Producto actualizado exitosamente: {ProductId}", id);

                return Ok(new
                {
                    success = true,
                    message = "Producto actualizado exitosamente",
                    data = MapToProductDto(product)
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Producto no encontrado para actualizar: {ProductId}", id);
                return NotFound(new { success = false, message = $"Producto con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al actualizar producto");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar producto");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Eliminar un producto (solo Admin)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteProduct(int id)
        {
            try
            {
                _logger.LogInformation("Eliminando producto con ID: {ProductId}", id);

                var request = new DeleteProductRequest { Id = id };
                var response = await _productClient.DeleteProductAsync(request);

                if (!response.Success)
                {
                    return NotFound(new { success = false, message = response.Message });
                }

                _logger.LogInformation("Producto eliminado exitosamente: {ProductId}", id);

                return Ok(new
                {
                    success = true,
                    message = "Producto eliminado exitosamente"
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Producto no encontrado para eliminar: {ProductId}", id);
                return NotFound(new { success = false, message = $"Producto con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al eliminar producto");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al eliminar producto");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Buscar productos por término de búsqueda
        /// </summary>
        [HttpGet("search")]
        [AllowAnonymous] // Este endpoint es público
        [ProducesResponseType(typeof(List<ProductDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> SearchProducts(
            [FromQuery] string query,
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                _logger.LogInformation("Buscando productos con query: {Query}", query);

                var request = new SearchProductsRequest
                {
                    SearchTerm = query,
                    PageNumber = pageNumber,
                    PageSize = pageSize
                };

                var products = new List<ProductDto>();
                var streamingCall = _productClient.SearchProducts(request);

                await foreach (var product in streamingCall.ResponseStream.ReadAllAsync())
                {
                    products.Add(MapToProductDto(product));
                }

                _logger.LogInformation("Se encontraron {Count} productos para query: {Query}",
                    products.Count, query);

                return Ok(new
                {
                    success = true,
                    data = products,
                    query,
                    totalCount = products.Count
                });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al buscar productos");
                return StatusCode(503, new { success = false, message = "Servicio de productos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al buscar productos");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        private static ProductDto MapToProductDto(ProductResponse product)
        {
            return new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Description = product.Description,
                Price = product.Price,
                Stock = product.Stock,
                Category = product.Category,
                IsActive = product.IsActive,
                CreatedAt = product.CreatedAt
            };
        }
    }

    // DTOs para ProductsController
    public class ProductDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public string CreatedAt { get; set; } = string.Empty;
    }

    public class CreateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
    }

    public class UpdateProductDto
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public double Price { get; set; }
        public int Stock { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }
}
