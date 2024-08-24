using Integrador.Domain.Cliente;
using Integrador.Domain.Email;
using Integrador.Domain.EmailConfigure;
using Integrador.Domain.LogIntegracao;
using Integrador.Domain.OnBloxConfigure;
using Integrador.Repository.Cliente;
using Integrador.Repository.Email;
using Integrador.Repository.EmailConfigure;
using Integrador.Repository.OnBloxConfigure;
using Integrador.Services;
using Integrador.Services.Cliente;
using Integrador.Services.Email;
using Integrador.Services.EmailConfigure;
using Integrador.Services.OnBloxConfigure;
using Integrador.WebService;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Text.Json.Serialization;
using System.Threading;
using System.Timers;
using System.Xml;

namespace IntegradorOnBloxService
{
    public class LogDados
    {
        public DateTime HoraExecucao { get; set; }
        public string Descricao { get; set; }
        //public TimeSpan HoraProgramada { get; set; }
        
    }

    public partial class Service : ServiceBase
    {
        //TODO: SETAR O TIMER PARA EXECUTAR O SERVIÇO
        private System.Timers.Timer _timer;
        int nextExecutionIndex = 0;

        //SERVIÇOS DE BUSCA DE DADOS
        private readonly OnBloxService _onBloxService;
        private readonly EmailConfigureService _emailConfigureService;
        private readonly EmailService _emailService;
        private readonly ClienteService _clienteService;
        private readonly JsonService _jsonService;

        //MODELS DE CONFIGURAÇÃO 
        private OnBloxConfigureModel _onBloxConfigureModel;
        private EmailConfigureModel _emailConfigureModel;

        //MODELS DE ENTIDADES
        private List<EmailModel> EmailModelList;
        private List<ClienteModel> ClienteModelLilst;
        List<TimeSpan> executionTimes = new List<TimeSpan>();

        public Service()
        {
            InitializeComponent();
            _emailConfigureService = new EmailConfigureService(new EmailConfigureRepository(new AppDbContext()));
            _emailService = new EmailService(new EmailRepository(new AppDbContext()), _emailConfigureService.GetEmailConfigure());
            _onBloxService = new OnBloxService(new OnBloxConfigureRepository(new AppDbContext()));
            _clienteService = new ClienteService(new ClienteRepository(new AppDbContext()));
            _jsonService = new JsonService();

        }


        private List<TimeSpan> SetTimer()
        {

            _onBloxConfigureModel = new OnBloxConfigureModel();
            _onBloxConfigureModel = _onBloxService.GetOnBloxConfigure();

            //// Altera os tempos de execução para 1 minuto, 2 minutos e 3 minutos após a hora atual
            //_onBloxConfigureModel.HoraExecucao01 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(1));
            //_onBloxConfigureModel.HoraExecucao02 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(2));
            //_onBloxConfigureModel.HoraExecucao03 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(3));  



            //CRIA UMA LISTA COM OS HORÁRIOS DE EXECUÇÃO
            List<TimeSpan> executionTimes = new List<TimeSpan>()
            {
                _onBloxConfigureModel.HoraExecucao01,
                _onBloxConfigureModel.HoraExecucao02,
                _onBloxConfigureModel.HoraExecucao03
            };




            return executionTimes;



        }
        private void SetarProximaExecucao(List<TimeSpan> timedList, int executionIndex)
        {
            //CALCULA O INTERVALO ATÉ A PRÓXIMA EXECUÇÃO
            TimeSpan timeToGo = timedList[executionIndex] - DateTime.Now.TimeOfDay;
            if (timeToGo <= TimeSpan.Zero)
            {
                //SE A HORA JÁ PASSOU, PROGRMAA PARA O PRÓXIMO HORÁRIO
                executionIndex = (executionIndex + 1) % timedList.Count;
                timeToGo = timedList[executionIndex] - DateTime.Now.TimeOfDay;
                if (timeToGo < TimeSpan.Zero)
                {
                    timeToGo = timeToGo.Add(new TimeSpan(24, 0, 00));
                }
            }

            _timer = new System.Timers.Timer(timeToGo.TotalMilliseconds);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = false; //EXECUTA APENAS UMA VEZ
            _timer.Start();
        }


        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {

            // hora da execução
            
            //EXECUTA O SERVIÇO
            ExecutaIntegrcao();

            //PROGRAMA O PRÓXIMO HORÁRIO
            nextExecutionIndex = (nextExecutionIndex + 1) % 3;//ATUALIZA O ÍNDICE PARA O PRÓXIMO HORÁRIO
            executionTimes = SetTimer();
            SetarProximaExecucao(executionTimes, nextExecutionIndex);
            SaveLogFile(DateTime.Now, "iniciando a integração");

        }

        protected override void OnStart(string[] args)
        {
            

            //PEGA AS HORAS DE EXECUÇÃO DO BANCO
            executionTimes = SetTimer();

            //DETERMINA QUANDO SERÁ EXECUTADO
            SetarProximaExecucao(executionTimes, nextExecutionIndex);

            SaveLogFile(DateTime.Now, "inicio da execucao");
        }
        public void ExecutaIntegrcao()
        {
            BuscaConfiguracoes();
            //ReceberEmails();
            SalvarClientesDoEmail();
            IntegrararClientes();
        }

        protected override void OnStop()
        {
            _timer.Stop();
            _timer.Dispose();
        }


        public void BuscaConfiguracoes()
        {

            _onBloxConfigureModel = new OnBloxConfigureModel();
            _emailConfigureModel = new EmailConfigureModel();
            _onBloxConfigureModel = _onBloxService.GetOnBloxConfigure();
            _emailConfigureModel = _emailConfigureService.GetEmailConfigure();
        }

        public void ReceberEmails()
        {
            EmailModelList = new List<EmailModel>();
            _emailService.ConnectHost(true);
            EmailModelList = _emailService.ReceberMensagens(
                this._emailConfigureModel.CaixaDeEmail,
                this._emailConfigureModel.AssuntoEmail);
            _emailService.SalvarEmailsNoBancoDeDados(this.EmailModelList);

        }

        public void SalvarClientesDoEmail()
        {
            _emailService.SalvarClienteNoBanco();
        }

        public void IntegrararClientes()
        {

            this.ClienteModelLilst = _clienteService.GetAll() as List<ClienteModel>;
            foreach (var item in ClienteModelLilst)
            {
                if (item != null)
                {
                    if (!item.integrado)
                    {
                        _jsonService.SendData(item);
                        _clienteService.SetIntegrado(item);

                    }
                }
            }


        }


        public async void SaveLogFile(DateTime horarecebida, string descricao)
        {
            _emailConfigureModel = new EmailConfigureModel();
            _emailConfigureModel = _emailConfigureService.GetEmailConfigure();

            LogDados log = new LogDados
            {
                HoraExecucao = horarecebida,
                Descricao = descricao,
                //HoraProgramada = horaprogramada
            };

            string logSerializado = JsonConvert.SerializeObject(log);
            StreamWriter file = null;
            try
            {
                file = File.AppendText($@"{_emailConfigureModel.PastaTemporaria}\LogService{DateTime.Now.Day:D2}{DateTime.Now.Month:D2}{DateTime.Now.Year}.json");
                await file.WriteAsync("\n" + logSerializado);
            }
            catch (Exception e)
            {
                throw new Exception($"Erro ao salvar o arquivo de Log. MessageError: {e.Message} \n InnerException: {e.InnerException}");
            }
            finally
            {
                file?.Close();
            }
        }

        public void OnDebug()
        {
            System.Diagnostics.Debugger.Launch();
            OnStart(null);
        }
    }
}
