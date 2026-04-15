import axios from 'axios';

const configuredBaseUrl =
  process.env.VUE_APP_BASE_API ||
  process.env.BASE_API ||
  'http://localhost:8080';

axios.defaults.baseURL = configuredBaseUrl;

const service = axios.create({
  timeout: 40000,
  headers: {
    'X-Requested-With': 'XMLHttpRequest',
    'Content-Type': 'application/json; charset=UTF-8'
  },
})

service.interceptors.request.use(
  config => {
    return config
  },
  error => {
    Promise.reject(error)
  }
)

service.interceptors.response.use(
  response => {
    const res = response.data;
    return res
  },
  error => {
  }
)

export default service
