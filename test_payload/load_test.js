import http from 'k6/http';
import { check, sleep } from 'k6';

export let options = {
    stages: [
        { duration: '30s', target: 10 }, 
        { duration: '1m', target: 50 }, 
        { duration: '30s', target: 0 }, 
    ],

    insecureSkipTLSVerify: true, 
};

const urls = [
    'https://localhost:7298/item/93',
    'https://localhost:7298/item/6',
    'https://localhost:7298/?page=4',
    'https://localhost:7298',
    'https://localhost:7298/?brand=2&type=4',
    'https://localhost:7298/?brand=3&type=3'
];

export default function () {
    for(const url of urls) {
        
        const res = http.get(url);
        
        check(res, {
            'is status 200': (r) => r.status === 200,
        });

        sleep(1);
    }
}