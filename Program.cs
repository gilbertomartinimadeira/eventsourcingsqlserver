using System.Data.SqlClient;
using System.Text;
using System.Text.Json;

namespace es
{
    public static class Program
    {
        public static void Main(String[] args)
        {                 
            var contaCorrente = new ContaCorrente(71);            

            contaCorrente.Depositar(10000);

            contaCorrente.Sacar(500);
            contaCorrente.Sacar(500);        
            contaCorrente.Sacar(500);
            contaCorrente.Sacar(500);        

            var connstring = "Data Source=localhost;Initial Catalog=eventstore;User Id=sa;Password=123Mudar;";

            using var contaCorrenteRepository = new ContaCorrenteRepository(connstring);

            contaCorrenteRepository.Salvar(contaCorrente);

            var contaRecuperadaDoEventStore = contaCorrenteRepository.Obter(71);            

        }       
    }

    public interface Evento 
    { 
        public DateTime DataOcorrencia { get; } 
        public string Nome { get; }
    }

    public class ContaCorrente
    {
        public ContaCorrente(int id)
        {
            Id = id;
        }

        public List<Evento> Eventos { get; private set; } = new List<Evento>();
        public int Id { get; private set; }
        public decimal Saldo { get; private set; }

        public void Sacar(decimal quantiaEmDinheiro) 
        {            
           if(quantiaEmDinheiro > Saldo) throw new ArgumentException("Saldo insuficiente");        
           RegistrarEvento(new Saque(Id, quantiaEmDinheiro, DateTime.Now));
        }
    
        public void Depositar(decimal quantiaEmDinheiro) 
        {                    
           RegistrarEvento(new Deposito(Id, quantiaEmDinheiro, DateTime.Now));
        }

        public void RegistrarEvento(Evento evento) 
        {
            switch (evento)
            {
                case Deposito deposito: Aplicar(deposito); break;

                case Saque saque: Aplicar(saque); break;
                
                default: throw new ArgumentException("Evento não suportado");
            }

            Eventos.Add(evento);
        }

        public void Aplicar(Deposito deposito)
        {
            Saldo += deposito.QuantiaEmDinheiro;
        }

        public void Aplicar(Saque saque)
        {
            Saldo -= saque.QuantiaEmDinheiro;
        }



    
    }

    public class Saque : Evento
    {
        public Saque(int idConta, decimal quantiaEmDinheiro, DateTime dataOcorrencia)
        {
            IdConta = idConta;
            QuantiaEmDinheiro = quantiaEmDinheiro;
            DataOcorrencia = dataOcorrencia;                    
        }

        public int IdConta { get;}
        public decimal QuantiaEmDinheiro { get; }
        public DateTime DataOcorrencia { get; }

        public string Nome => nameof(Saque);

        public override string ToString()
        {
            return JsonSerializer.Serialize(new { QuantiaEmDinheiro = QuantiaEmDinheiro });
        }

    }

    public class Deposito : Evento
    {
        public Deposito(int idConta, decimal quantiaEmDinheiro, DateTime dataOcorrencia)
        {
            IdConta = idConta;
            QuantiaEmDinheiro = quantiaEmDinheiro;
            DataOcorrencia = dataOcorrencia;
        
        }

        public int IdConta { get; set; }
        public decimal QuantiaEmDinheiro { get; set; }
        public DateTime DataOcorrencia { get; }

        public string Nome => nameof(Deposito);

        public override string ToString()
        {
            return JsonSerializer.Serialize(new { QuantiaEmDinheiro = QuantiaEmDinheiro });
        }
    }

    public class ContaCorrenteRepository : IDisposable
    {
        public SqlConnection Conexao { get; set; }
        public SqlCommand? Comando { get; set; }    
        public ContaCorrenteRepository(string connectionString)
        {
            Conexao = new SqlConnection(connectionString);

        }
        private Dictionary <int, List<Evento>> _eventStore = new Dictionary<int, List<Evento>>();
        public void Salvar(ContaCorrente contaCorrente)
        {
               
            if(!contaCorrente.Eventos.Any()) return;

            EventStream eventStream;
            StringBuilder comandText = new StringBuilder($"insert into [eventstream] values ");

            foreach (var evento in contaCorrente.Eventos)
            {
                eventStream = new EventStream(  stream_id : contaCorrente.Id, 
                                               event_name : evento.Nome,
                                               event_info : evento.ToString() ?? string.Empty,
                                               created_at : evento.DataOcorrencia );

                comandText.Append($"({contaCorrente.Id},'{eventStream.event_name}','{ eventStream.event_info}', '{eventStream.created_at.ToString("yyyy/MM/dd HH:mm:ss.fff")}'),");    
            }            

            var text = comandText.ToString();

            text = text.Remove(text.Length -1);

            try
            {
                Conexao.Open();
                Comando = new SqlCommand(text, Conexao);
                Comando.ExecuteNonQuery();
            }
            catch (System.Exception ex)
            {
                System.Console.WriteLine( ex.Message);                
            }finally
            {
                Conexao.Close();
            }
            
            //_eventStore[contaCorrente.Id] = contaCorrente.Eventos;
        }

        public ContaCorrente Obter(int id)
        {
            SqlDataReader reader;
            var cmdText = $"select * from eventstream where stream_id = {id}";

            try
            {
                Conexao.Open();
                Comando = new SqlCommand(cmdText,Conexao);
                reader = Comando.ExecuteReader();
                List<EventStream> eventList = new List<EventStream>();
                
                while(reader.Read())
                {
                    var stream_id = Convert.ToInt32(reader["stream_id"]);
                    var event_name = reader["event_name"].ToString() ?? "";
                    var event_info = reader["event_info"].ToString() ?? "";
                    var created_at = Convert.ToDateTime(reader["created_at"]);


                    eventList.Add(new EventStream(stream_id, event_name, event_info, created_at));
                }

                var contaCorrente = new ContaCorrente(id);

                foreach (var dbEvent in eventList)
                {
                    var evento = dbEvent.DeserializarEvento();

                    contaCorrente.RegistrarEvento(evento);
                }

                return contaCorrente;


            }
            catch (System.Exception)
            {
                
                throw;
            }
            finally
            {
                Conexao.Close();
            }
          
            



            // var contaCorrente = new ContaCorrente(id);

            // foreach(var evento in _eventStore[contaCorrente.Id])
            // {
            //     contaCorrente.RegistrarEvento(evento);
            // }

            // return contaCorrente;
        }

        public void Dispose()
        {
            if(Conexao?.State != System.Data.ConnectionState.Closed ) Conexao?.Close();
        }

    }

    public class EventStream 
    {
        public int stream_id {get;}
        public string event_name{get;}
        public string event_info{get;}
        public DateTime created_at{get;}


        public EventStream(int stream_id, string event_name, string event_info, DateTime created_at)
        {
            this.stream_id = stream_id;
            this.event_name = event_name;
            this.event_info = event_info;
            this.created_at = created_at;
        }

            class EventInfo
            {
                public decimal QuantiaEmDinheiro{get;set;}
            }
        public Evento? DeserializarEvento()
        {
            
            Evento? evento = null;
            switch(event_name)
            {
                case "Saque" :                
                    var valueObject = JsonSerializer.Deserialize<EventInfo>(event_info) ?? new EventInfo();
                    evento = new Saque(stream_id, valueObject.QuantiaEmDinheiro,created_at);
                break;

                case "Deposito" :                
                    valueObject = JsonSerializer.Deserialize<EventInfo>(event_info) ?? new EventInfo();
                    evento = new Deposito(stream_id, valueObject.QuantiaEmDinheiro,created_at);
                break;
            }

            return evento;
            
        }
    }

}
