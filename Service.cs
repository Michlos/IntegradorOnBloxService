using Integrador.Domain.Cliente;
using Integrador.Domain.Email;
using Integrador.Domain.EmailConfigure;
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

using System;
using System.Collections.Generic;
using System.IO;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace IntegradorOnBloxService
{

    public partial class Service : ServiceBase
    {
        //TODO: SETAR O TIMER PARA EXECUTAR O SERVIÇO
        private System.Timers.Timer _timer;
        int nextExecutionIndex = 0;

        //SERVIÇOS DE BUSCA DE DADOS
        private readonly OnBloxService _onBloxService;
        private EmailConfigureService _emailConfigureService;
        private EmailService _emailService;
        private ClienteService _clienteService;
        private JsonService _jsonService;

        //MODELS DE CONFIGURAÇÃO 
        private OnBloxConfigureModel _onBloxConfigureModel;
        private EmailConfigureModel _emailConfigureModel;

        //MODELS DE ENTIDADES
        private List<EmailModel> EmailModelList;
        private ClienteModel ClienteModel;
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
            //ClienteModel = new ClienteModel();
            
            
            //_emailConfigureModel = new EmailConfigureModel();
            //_emailConfigureModel = _emailConfigureService.GetEmailConfigure();
        }


        //private List<TimeSpan> executionTimes = new List<TimeSpan>()
        //{

        //};

        private List<TimeSpan> SetTimer()
        {
            
            _onBloxConfigureModel = new OnBloxConfigureModel();
            _onBloxConfigureModel = _onBloxService.GetOnBloxConfigure();

            // Altera os tempos de execução para 1 minuto, 2 minutos e 3 minutos após a hora atual
            _onBloxConfigureModel.HoraExecucao01 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(1));
            _onBloxConfigureModel.HoraExecucao02 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(2));
            _onBloxConfigureModel.HoraExecucao03 = DateTime.Now.TimeOfDay.Add(TimeSpan.FromMinutes(3));

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
            //EXECUTA O SERVIÇO
            


            //clocar o executaintegracao aqui
            ExecutaIntegrcao();
            //PROGRAMA O PRÓXIMO HORÁRIO
            nextExecutionIndex = (nextExecutionIndex + 1) % 3;//ATUALIZA O ÍNDICE PARA O PRÓXIMO HORÁRIO
            
            
            executionTimes = SetTimer();
            SetarProximaExecucao(executionTimes, nextExecutionIndex);


        }

        protected override void OnStart(string[] args)
        {
            
            
            //PEGA AS HORAS DE EXECUÇÃO DO BANCO
            executionTimes = SetTimer();
            
            //DETERMINA QUANDO SERÁ EXECUTADO
            SetarProximaExecucao(executionTimes, nextExecutionIndex);
            
        }
        public void ExecutaIntegrcao()
        {
            BuscaConfiguracoes();
            ReceberEmails();
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
            this._onBloxConfigureModel = _onBloxService.GetOnBloxConfigure();
            this._emailConfigureModel = _emailConfigureService.GetEmailConfigure();
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
            //this.ClienteModelLilst = new List<ClienteModel>();
            this.ClienteModelLilst = _clienteService.GetAll() as List<ClienteModel>;
            //foreach (var item in clienteList)
            //{
            //    this.ClienteModelLilst.Add(item as ClienteModel);
            //}

            foreach (var item in ClienteModelLilst)
            {
                if (item != null)
                {

                    _jsonService.SendData(item);
                    _clienteService.SetIntegrado(item);                       
                }
            }


        }

        public void onDebug()
        {
            //System.Diagnostics.Debugger.Launch();
            OnStart(null);
        }
    }
}
