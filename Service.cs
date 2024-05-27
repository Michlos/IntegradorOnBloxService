using Integrador.Domain.Cliente;
using Integrador.Domain.Email;
using Integrador.Domain.EmailConfigure;
using Integrador.Domain.OnBloxConfigure;
using Integrador.Services.Cliente;
using Integrador.Services.Email;
using Integrador.Services.EmailConfigure;
using Integrador.Services.OnBloxConfigure;
using Integrador.WebService;

using System;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading;
using System.Timers;

namespace IntegradorOnBloxService
{

    public partial class Service : ServiceBase
    {
        //TODO: SETAR O TIMER PARA EXECUTAR O SERVIÇO
        private System.Timers.Timer _timer;

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

        public Service()
        {
            InitializeComponent();  
            _onBloxConfigureModel = new OnBloxConfigureModel();
            _emailConfigureModel = new EmailConfigureModel();
            ClienteModel = new ClienteModel();
        }

        private void SetTimer()
        {
            //CALCULA O INTERVALO ATÉ A PRÓXIMA EXECUÇÃO
            TimeSpan timeToGo = _onBloxConfigureModel.HoraExecucao - DateTime.Now.TimeOfDay;
            if (timeToGo < TimeSpan.Zero)
            {
                //SE A HORA JÁ PASSOU, PROGRAMA PARA O PRÓXIMO DIA
                timeToGo = timeToGo.Add(new TimeSpan(24, 0, 0));
            }

            _timer = new System.Timers.Timer(timeToGo.TotalMilliseconds);
            _timer.Elapsed += OnTimedEvent;
            _timer.AutoReset = false; //EXECUTA APENAS UMA VEZ
            _timer.Start();
        }

        private void OnTimedEvent(object sender, ElapsedEventArgs e)
        {
            BuscaConfiguracoes();
            ReceberEmails();
            SalvarClientesDoEmail(); 
            IntegrararClientes();

        }

        protected override void OnStart(string[] args)
        {

            SetTimer();
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
            this.ClienteModelLilst = new List<ClienteModel>();
            var clienteList = _clienteService.GetAll();
            foreach (var item in clienteList)
            {
                this.ClienteModelLilst.Add(item as ClienteModel);
            }

            foreach (var item in this.ClienteModelLilst)
            {
                if (item != null)
                {

                    _jsonService.SendData(item);
                    _clienteService.SetIntegrado(item);                       
                }
            }


        }
    }
}
