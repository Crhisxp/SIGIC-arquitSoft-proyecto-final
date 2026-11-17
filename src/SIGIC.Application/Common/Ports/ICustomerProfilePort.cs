namespace SIGIC.Application.Common.Ports;

/// <summary>
/// Puerto para resolver el perfil/rol homologado del cliente (RF-PRE-001, RF-PRE-002).
/// Previene manipulación maliciosa de RolCliente desde el cliente HTTP o terminal POS.
/// </summary>
public interface ICustomerProfilePort
{
    Task<string> ObtenerPerfilClienteAsync(Guid? clienteId, CancellationToken cancellationToken = default);
}
