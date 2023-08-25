using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Polly;

namespace Test_Polly_Framework4_8
{
    internal class Program
    {
        const int NRO_REINTENTOS = 3;

        static void Main(string[] args)
        {
            try
            {
                //DESCOMENTAR PARA PROBAR

                //RetryAnteUnaException();
                //RetryAnteUnaExceptionConAction();
                //RetryAnteMultiplesTiposDeExceptions();
                //RetryAnteExceptionYResultadoInvalido();
                //RetryConBackOff();
                //Console.WriteLine("Valor obtenido: " + Fallback());
                //Console.WriteLine("Valor obtenido: " + RetryMasFallback());
                RetryConHttpClientNoAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
            }

            Console.WriteLine("Presione una tecla para salir...");
            Console.ReadKey();
        }


        public static void RetryAnteUnaException()
        {
            var retryPolicy = Policy.Handle<Exception>().Retry(3);

            retryPolicy.Execute(() =>
            {
                EjecutarProceso(2);
            });
        }


        public static void RetryAnteUnaExceptionConAction()
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry(3, (ex, nroReintento) => {
                    Console.WriteLine($"La operacion fallo reintento {nroReintento} " +
                        $"de {NRO_REINTENTOS}. Excepcion: {ex.Message} ");
                });

            retryPolicy.Execute(() =>
            {
                EjecutarProceso(2);
            });
        }


        public static void RetryAnteMultiplesTiposDeExceptions()
        {
            var retryPolicy = Policy
                .Handle<NullReferenceException>()
                .Or<FormatException>()
                .Or<NullReferenceException>()
                .OrInner<ArgumentOutOfRangeException>() //Detecta si hay alguna Inner del tipo determinado.
                .Retry(3, (ex, nroReintento) => {
                    Console.WriteLine($"La operacion fallo reintento {nroReintento} " +
                        $"de {NRO_REINTENTOS}. Excepcion: {ex.Message} ");
                });

            retryPolicy.Execute(() =>
            {
                EjecutarProceso(1);
            });
        }


        public static void RetryAnteExceptionYResultadoInvalido()
        {
            const int VALOR_INCORRECTO = 1;

            var retryPolicy = Policy
                .Handle<NullReferenceException>()
                .OrResult<int>(resultado => resultado == VALOR_INCORRECTO)    //Si propone que si el resultado es 1 se considere falla
                .Retry(NRO_REINTENTOS, (excepcionOResultado, nroReintento) => {

                    //Si el reintento NO fue por una Excepcion, entonces excepcionOResultado.Exception
                    // valdrá null.
                    if (excepcionOResultado.Exception != null)
                    {
                        Console.WriteLine($"Ocurrio una excepcion  {nameof(excepcionOResultado.Exception)}. " +
                            $"Msje: {excepcionOResultado.Exception.Message}");
                    }

                    //Si el reintento fue por una Excepcion, entonces excepcionOResultado.Result
                    // será el valor default del tipo usado con .OnResult, en este caso un tipo int
                    // es decir, para el caso será 0, caso contrario, será el valor correspondiente
                    if (excepcionOResultado.Result != 0)
                    {
                        Console.WriteLine("La función retornó un codigo no valido");
                    }
                    Console.WriteLine($"La operacion fallo reintento {nroReintento} " +
                        $"de {NRO_REINTENTOS}.");
                });

            //De ser necesario, se puede leer el valor de EjecutarProceso()
            int returnedValue = retryPolicy.Execute(() =>
            {
                return EjecutarProceso(1);  //La funcion devolvera un valor que la policy detectara como erroneo.
            });
        }


        public static void RetryConBackOff()
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(new[]
                {
                    TimeSpan.FromSeconds(2),
                    TimeSpan.FromSeconds(6),
                    TimeSpan.FromSeconds(10)
                });

            retryPolicy.Execute(() =>
            {
                EjecutarProceso(2);
            });
        }

        public static void RetryConBackOffExponencial()
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetry(NRO_REINTENTOS, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));

            retryPolicy.Execute(() =>
            {
                EjecutarProceso(2);
            });
        }


        //Devuelve un valor predeterminado ante una falla
        public static int Fallback()
        {

            var fallbackPolicy = Policy<int>
                .Handle<Exception>()
                //.Fallback((action) => 100);       //Otra forma mas corta
                .Fallback((action) => 
                {
                    return 100; // Valor de respaldo en caso de fallo
                });

            var result = fallbackPolicy.Execute(() =>
            {
                return EjecutarProceso(2);
            });

            return result;
        }


        //Al agotar los reintentos, devuelve un valor por default
        //Se logra enganchando una politica de Fallback con una de Retry
        public static int RetryMasFallback()
        {
            var retryPolicy = Policy
                .Handle<Exception>()
                .Retry(3, (ex, nroReintento) => {
                    Console.WriteLine($"Reintento #{nroReintento} de {NRO_REINTENTOS}. Excepcion: {ex.Message} ");
                });

            var fallbackPolicy = Policy<int>
                .Handle<Exception>()
                .Fallback((action) => 100);

            var result = fallbackPolicy.Wrap(retryPolicy).Execute(() =>
            {
                return EjecutarProceso(2);
            });

            return result;
        }

        private static int EjecutarProceso(int parametro)
        {

            Console.WriteLine("Ejecutando Proceso...");
            if (parametro == 0)
                return 0;
            else if (parametro == 1)
                return 1;
            else
                throw new Exception("Error al ejecutar!!"
                    , new FormatException("Es una format Exception"
                        , new ArgumentOutOfRangeException("Una Excepcion de OutOfRange")
                        )
                    );
        }
        
        static async void RetryConHttpClientNoAsync()
        {
            var httpClient = new HttpClient();

            // Definir política de reintentos y espera exponencial
            var retryPolicy = Policy
                .Handle<Exception>()
                .OrResult<HttpResponseMessage>(respuesta => !respuesta.IsSuccessStatusCode)
                .WaitAndRetry(NRO_REINTENTOS -1
                    , retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
                    , (httpResponse, nroReintento) => Console.WriteLine($"Respuesta no satisfactoria Http Code: {httpResponse.Result.StatusCode} Reintentando nuevamente")
                    );

            // Ejecutar la llamada HTTP con la política
            HttpResponseMessage response = retryPolicy.Execute(  () =>
            {
                //Pagina que siempre devuelve error 404
                return httpClient.GetAsync("https://httpbin.org/status/404").GetAwaiter().GetResult();
            });
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine("Respuesta exitosa: " + response.StatusCode);
            }
            else
            {
                Console.WriteLine("Error en la llamada HTTP: " + response.StatusCode);
            }
        }

        private static HttpResponseMessage GetHttpResponse(int parametro)
        {

            Console.WriteLine("Ejecutando Proceso...");
            if (parametro == 0)
                return new HttpResponseMessage(HttpStatusCode.OK);
            else if (parametro == 1)
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            else
                throw new Exception("Error al ejecutar!!"
                    , new FormatException("Es una format Exception"
                        , new ArgumentOutOfRangeException("Una Excepcion de OutOfRange")
                        )
                    );
        }
    }
}
