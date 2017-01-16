namespace ShortBus
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;

    public interface IMediator
    {
        Response<TResponseData> Request<TResponseData>(IRequest<TResponseData> request);
        Task<Response<TResponseData>> RequestAsync<TResponseData>(IAsyncRequest<TResponseData> query);

        Response Notify<TNotification>(TNotification notification);
        Task<Response> NotifyAsync<TNotification>(TNotification notification);
    }

    public class Mediator : IMediator
    {
        readonly IDependencyResolver _dependencyResolver;

        public Mediator(IDependencyResolver dependencyResolver)
        {
            _dependencyResolver = dependencyResolver;
        }

        private List<RequestInterceptor> GetRequestInterceptors<TResponseData>(MediatorPlan<TResponseData> plan)
        {
            var implementationMethod = plan.HandlerInstance.GetType().GetTypeInfo()
                  .GetMethod(plan.HandleMethod.Name,
                      plan.HandleMethod.GetParameters().Select(info => info.ParameterType).ToArray());

            var interceptors = new List<RequestInterceptor>();
            foreach (var attribute in implementationMethod.GetCustomAttributes())
            {
                if (!(attribute is RequestInterceptAttribute)) continue;
                foreach (var interceptor in ((RequestInterceptAttribute)attribute).GetInterceptors())
                {
                    var requestInterceptor = (RequestInterceptor)_dependencyResolver.GetInstance(interceptor);
                    requestInterceptor.RequestInterceptAttribute = (RequestInterceptAttribute)attribute;
                    interceptors.Add(requestInterceptor);
                }
            }

            return interceptors;
        }

        public virtual Response<TResponseData> Request<TResponseData>(IRequest<TResponseData> request)
        {
            var response = new Response<TResponseData>();

            try
            {
                var plan = new MediatorPlan<TResponseData>(typeof (IRequestHandler<,>), "Handle", request.GetType(), _dependencyResolver);

                var interceptors = GetRequestInterceptors(plan);

                foreach (var interceptor in interceptors)
                {
                    interceptor.BeforeInvoke(plan.HandleMethod, request, request.GetType());
                }

                response.Data = plan.Invoke(request);

                foreach (var interceptor in interceptors)
                {
                    interceptor.AfterInvoke(plan.HandleMethod, request, request.GetType(), response.Data);
                }
            }
            catch (Exception e)
            {
                response.Exception = e;
            }

            return response;
        }

        public async Task<Response<TResponseData>> RequestAsync<TResponseData>(IAsyncRequest<TResponseData> query)
        {
            var response = new Response<TResponseData>();

            try
            {
                var plan = new MediatorPlan<TResponseData>(typeof (IAsyncRequestHandler<,>), "HandleAsync", query.GetType(), _dependencyResolver);

                var interceptors = GetRequestInterceptors(plan);

                foreach (var interceptor in interceptors)
                {
                   await interceptor.BeforeInvokeAsync(plan.HandleMethod,query,query.GetType());
                }

                response.Data = await plan.InvokeAsync(query);

                foreach (var interceptor in interceptors)
                {
                    await interceptor.AfterInvokeAsync(plan.HandleMethod, query, query.GetType(), response.Data);
                }
            }
            catch (Exception e)
            {
                response.Exception = e;
            }

            return response;
        }

        public Response Notify<TNotification>(TNotification notification)
        {
            var handlers = _dependencyResolver.GetInstances<INotificationHandler<TNotification>>();

            var response = new Response();
            List<Exception> exceptions = null;

            foreach (var handler in handlers)
                try
                {
                    handler.Handle(notification);
                }
                catch (Exception e)
                {
                    ( exceptions ?? ( exceptions = new List<Exception>() ) ).Add(e);
                }
            if (exceptions != null)
                response.Exception = new AggregateException(exceptions);
            return response;
        }

        public async Task<Response> NotifyAsync<TNotification>(TNotification notification)
        {
            var handlers = _dependencyResolver.GetInstances<IAsyncNotificationHandler<TNotification>>();

            return await Task
                .WhenAll(handlers.Select(x => notifyAsync(x, notification)))
                .ContinueWith(task =>
                {
                    var exceptions = task.Result.Where(exception => exception != null).ToArray();
                    var response = new Response();

                    if (exceptions.Any())
                    {
                        response.Exception = new AggregateException(exceptions);
                    }

                    return response;
                });
        }

        static async Task<Exception> notifyAsync<TNotification>(IAsyncNotificationHandler<TNotification> asyncCommandHandler, TNotification message)
        {
            try
            {
                await asyncCommandHandler.HandleAsync(message);
            }
            catch (Exception e)
            {
                return e;
            }

            return null;
        }

        class MediatorPlan<TResult>
        {
            public MethodInfo HandleMethod;
            public readonly Func<object> HandlerInstanceBuilder;
            public object HandlerInstance;

            public MediatorPlan(Type handlerTypeTemplate, string handlerMethodName, Type messageType, IDependencyResolver dependencyResolver)
            {
                var handlerType = handlerTypeTemplate.MakeGenericType(messageType, typeof (TResult));

                HandleMethod = GetHandlerMethod(handlerType, handlerMethodName, messageType);

                HandlerInstanceBuilder = () => dependencyResolver.GetInstance(handlerType);

                HandlerInstance = HandlerInstanceBuilder();
            }

            MethodInfo GetHandlerMethod(Type handlerType, string handlerMethodName, Type messageType)
            {
                return handlerType.GetTypeInfo()
                    .GetMethod(handlerMethodName,
                        BindingFlags.Public | BindingFlags.Instance | BindingFlags.InvokeMethod);
            }

            public TResult Invoke(object message)
            {
                return (TResult) HandleMethod.Invoke(HandlerInstance, new[] { message });
            }

            public async Task<TResult> InvokeAsync(object message)
            {
                return await (Task<TResult>) HandleMethod.Invoke(HandlerInstance, new[] { message });
            }
        }


    }
}