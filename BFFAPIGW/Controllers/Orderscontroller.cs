using ECommerceGRPC.OrderService;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace BFFAPIGW.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // Requiere autenticación JWT para todos los endpoints
    public class OrdersController : ControllerBase
    {
        private readonly OrderService.OrderServiceClient _orderClient;
        private readonly ILogger<OrdersController> _logger;

        public OrdersController(
            OrderService.OrderServiceClient orderClient,
            ILogger<OrdersController> logger)
        {
            _orderClient = orderClient;
            _logger = logger;
        }

        /// <summary>
        /// Obtener todos los pedidos (solo Admin) o los pedidos del usuario actual
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(List<OrderDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetOrders(
            [FromQuery] int pageNumber = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var userRole = GetUserRoleFromToken();

                _logger.LogInformation("Obteniendo pedidos - Usuario: {UserId}, Rol: {Role}",
                    userId, userRole);

                var orders = new List<OrderDto>();

                // Si es Admin, obtiene todos los pedidos. Si no, solo sus propios pedidos
                if (userRole == "Admin")
                {
                    var request = new GetOrdersRequest
                    {
                        PageNumber = pageNumber,
                        PageSize = pageSize,
                        Status = status ?? string.Empty
                    };

                    var streamingCall = _orderClient.GetOrders(request);

                    await foreach (var order in streamingCall.ResponseStream.ReadAllAsync())
                    {
                        orders.Add(MapToOrderDto(order));
                    }
                }
                else
                {
                    // Usuario normal solo ve sus propios pedidos
                    var request = new GetOrdersByUserRequest
                    {
                        UserId = userId,
                        PageNumber = pageNumber,
                        PageSize = pageSize
                    };

                    var streamingCall = _orderClient.GetOrdersByUser(request);

                    await foreach (var order in streamingCall.ResponseStream.ReadAllAsync())
                    {
                        orders.Add(MapToOrderDto(order));
                    }
                }

                _logger.LogInformation("Se obtuvieron {Count} pedidos", orders.Count);

                return Ok(new
                {
                    success = true,
                    data = orders,
                    pageNumber,
                    pageSize,
                    totalCount = orders.Count
                });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al obtener pedidos");
                return StatusCode(503, new { success = false, message = "Servicio de pedidos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener pedidos");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Obtener un pedido por ID
        /// </summary>
        [HttpGet("{id}")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetOrder(int id)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var userRole = GetUserRoleFromToken();

                _logger.LogInformation("Obteniendo pedido con ID: {OrderId}", id);

                var request = new GetOrderRequest { Id = id };
                var order = await _orderClient.GetOrderAsync(request);

                // Verificar que el usuario tenga permiso para ver este pedido
                if (userRole != "Admin" && order.UserId != userId)
                {
                    _logger.LogWarning("Usuario {UserId} intentó acceder al pedido {OrderId} sin autorización",
                        userId, id);
                    return Forbid();
                }

                return Ok(new
                {
                    success = true,
                    data = MapToOrderDto(order)
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Pedido no encontrado: {OrderId}", id);
                return NotFound(new { success = false, message = $"Pedido con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al obtener pedido");
                return StatusCode(503, new { success = false, message = "Servicio de pedidos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al obtener pedido");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Crear un nuevo pedido
        /// </summary>
        [HttpPost]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderDto dto)
        {
            try
            {
                var userId = GetUserIdFromToken();

                _logger.LogInformation("Creando pedido para usuario: {UserId}", userId);

                var request = new CreateOrderRequest
                {
                    UserId = userId,
                    ShippingAddress = dto.ShippingAddress
                };

                foreach (var item in dto.Items)
                {
                    request.Items.Add(new OrderItemRequest
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity
                    });
                }

                var order = await _orderClient.CreateOrderAsync(request);

                _logger.LogInformation("Pedido creado exitosamente con ID: {OrderId}", order.Id);

                return CreatedAtAction(
                    nameof(GetOrder),
                    new { id = order.Id },
                    new
                    {
                        success = true,
                        message = "Pedido creado exitosamente",
                        data = MapToOrderDto(order)
                    });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.InvalidArgument)
            {
                _logger.LogWarning("Datos de pedido inválidos: {Details}", ex.Status.Detail);
                return BadRequest(new { success = false, message = ex.Status.Detail });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al crear pedido");
                return StatusCode(503, new { success = false, message = "Servicio de pedidos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al crear pedido");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Actualizar el estado de un pedido (solo Admin)
        /// </summary>
        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusDto dto)
        {
            try
            {
                _logger.LogInformation("Actualizando estado del pedido {OrderId} a {Status}", id, dto.Status);

                var request = new UpdateOrderStatusRequest
                {
                    Id = id,
                    Status = dto.Status
                };

                var order = await _orderClient.UpdateOrderStatusAsync(request);

                _logger.LogInformation("Estado del pedido actualizado exitosamente: {OrderId}", id);

                return Ok(new
                {
                    success = true,
                    message = "Estado del pedido actualizado exitosamente",
                    data = MapToOrderDto(order)
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Pedido no encontrado para actualizar: {OrderId}", id);
                return NotFound(new { success = false, message = $"Pedido con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al actualizar estado del pedido");
                return StatusCode(503, new { success = false, message = "Servicio de pedidos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al actualizar estado del pedido");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        /// <summary>
        /// Cancelar un pedido
        /// </summary>
        [HttpPost("{id}/cancel")]
        [ProducesResponseType(typeof(OrderDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CancelOrder(int id, [FromBody] CancelOrderDto dto)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var userRole = GetUserRoleFromToken();

                _logger.LogInformation("Cancelando pedido {OrderId} por usuario {UserId}", id, userId);

                // Primero obtener el pedido para verificar permisos
                var getRequest = new GetOrderRequest { Id = id };
                var existingOrder = await _orderClient.GetOrderAsync(getRequest);

                // Verificar que el usuario tenga permiso para cancelar este pedido
                if (userRole != "Admin" && existingOrder.UserId != userId)
                {
                    _logger.LogWarning("Usuario {UserId} intentó cancelar el pedido {OrderId} sin autorización",
                        userId, id);
                    return Forbid();
                }

                var request = new CancelOrderRequest
                {
                    Id = id,
                    Reason = dto.Reason ?? "Cancelado por el usuario"
                };

                var order = await _orderClient.CancelOrderAsync(request);

                _logger.LogInformation("Pedido cancelado exitosamente: {OrderId}", id);

                return Ok(new
                {
                    success = true,
                    message = "Pedido cancelado exitosamente",
                    data = MapToOrderDto(order)
                });
            }
            catch (RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogWarning("Pedido no encontrado para cancelar: {OrderId}", id);
                return NotFound(new { success = false, message = $"Pedido con ID {id} no encontrado" });
            }
            catch (RpcException ex)
            {
                _logger.LogError(ex, "Error gRPC al cancelar pedido");
                return StatusCode(503, new { success = false, message = "Servicio de pedidos no disponible" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error inesperado al cancelar pedido");
                return StatusCode(500, new { success = false, message = "Error interno del servidor" });
            }
        }

        private int GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value;

            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out int userId))
            {
                throw new UnauthorizedAccessException("Token inválido o usuario no encontrado");
            }

            return userId;
        }

        private string GetUserRoleFromToken()
        {
            return User.FindFirst(ClaimTypes.Role)?.Value ?? "Customer";
        }

        private static OrderDto MapToOrderDto(OrderResponse order)
        {
            return new OrderDto
            {
                Id = order.Id,
                UserId = order.UserId,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                ShippingAddress = order.ShippingAddress,
                CreatedAt = order.CreatedAt,
                CompletedAt = order.CompletedAt,
                Items = order.Items.Select(item => new OrderItemDto
                {
                    Id = item.Id,
                    ProductId = item.ProductId,
                    ProductName = item.ProductName,
                    Quantity = item.Quantity,
                    UnitPrice = item.UnitPrice,
                    Subtotal = item.Subtotal
                }).ToList()
            };
        }
    }

    // DTOs para OrdersController
    public class OrderDto
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public double TotalAmount { get; set; }
        public string ShippingAddress { get; set; } = string.Empty;
        public string CreatedAt { get; set; } = string.Empty;
        public string CompletedAt { get; set; } = string.Empty;
        public List<OrderItemDto> Items { get; set; } = new();
    }

    public class OrderItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public double UnitPrice { get; set; }
        public double Subtotal { get; set; }
    }

    public class CreateOrderDto
    {
        public string ShippingAddress { get; set; } = string.Empty;
        public List<CreateOrderItemDto> Items { get; set; } = new();
    }

    public class CreateOrderItemDto
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

    public class UpdateOrderStatusDto
    {
        public string Status { get; set; } = string.Empty;
    }

    public class CancelOrderDto
    {
        public string? Reason { get; set; }
    }
}
