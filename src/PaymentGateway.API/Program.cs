using PaymentGateway.API.Middleware;
using PaymentGateway.Application;
using PaymentGateway.Infrastructure;
using Serilog;
using Serilog.Events;
using Serilog.Sinks.MSSqlServer;
using System.Collections.ObjectModel;
using System.Data;

var builder = WebApplication.CreateBuilder(args);

// ── Serilog ───────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithEnvironmentName()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .WriteTo.Console()
    .WriteTo.MSSqlServer(
        connectionString: builder.Configuration.GetConnectionString("PaymentDb"),
        sinkOptions: new MSSqlServerSinkOptions
        {
            SchemaName = "Payment",
            TableName = "Logs",
            AutoCreateSqlTable = false
        },
        columnOptions: BuildSerilogColumnOptions())
    .CreateLogger();

builder.Host.UseSerilog();

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "PaymentGateway API", Version = "v1" });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory,
        $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml"),
        includeControllerXmlComments: true);
});

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// ── App ───────────────────────────────────────────────────────────────────────
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();

// ── Serilog column helper ─────────────────────────────────────────────────────
static ColumnOptions BuildSerilogColumnOptions()
{
    var cols = new ColumnOptions();
    cols.Store.Remove(StandardColumn.Properties);
    cols.Store.Remove(StandardColumn.MessageTemplate);
    cols.Store.Add(StandardColumn.LogEvent);

    cols.AdditionalColumns = new Collection<SqlColumn>
    {
        new() { ColumnName = "SourceContext",  DataType = SqlDbType.NVarChar, DataLength = 256  },
        new() { ColumnName = "RequestPath",    DataType = SqlDbType.NVarChar, DataLength = 512  },
        new() { ColumnName = "RequestMethod",  DataType = SqlDbType.NVarChar, DataLength = 10   },
        new() { ColumnName = "StatusCode",     DataType = SqlDbType.Int                          },
        new() { ColumnName = "MachineName",    DataType = SqlDbType.NVarChar, DataLength = 128  },
        new() { ColumnName = "EnvironmentName",DataType = SqlDbType.NVarChar, DataLength = 50   },
        new() { ColumnName = "ThreadId",       DataType = SqlDbType.Int                          },
        new() { ColumnName = "ProcessId",      DataType = SqlDbType.Int                          },
        new() { ColumnName = "TransactionId",  DataType = SqlDbType.NVarChar, DataLength = 128  },
        new() { ColumnName = "GatewayType",    DataType = SqlDbType.NVarChar, DataLength = 50   },
        new() { ColumnName = "CustomerId",     DataType = SqlDbType.NVarChar, DataLength = 128  },
        new() { ColumnName = "OrderNumber",    DataType = SqlDbType.NVarChar, DataLength = 128  },
        new() { ColumnName = "UserAgent",      DataType = SqlDbType.NVarChar, DataLength = 512  },
        new() { ColumnName = "RemoteIP",       DataType = SqlDbType.NVarChar, DataLength = 45   },
        new() { ColumnName = "RequestHost",    DataType = SqlDbType.NVarChar, DataLength = 256  },
    };

    return cols;
}
