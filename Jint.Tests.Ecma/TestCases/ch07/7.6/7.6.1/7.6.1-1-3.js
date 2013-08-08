/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-3.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: instanceof, typeof, else
 */


function testcase(){      
        var tokenCodes  = { 
            instanceof: 0,
            typeof: 1,
            else: 2
        };
        var arr = [
            'instanceof',
            'typeof',
            'else'
        ];        
        for(var p in tokenCodes) {
            for(var p1 in arr) {
                if(arr[p1] === p) {                     
                    if(!tokenCodes.hasOwnProperty(arr[p1])) {
                        return false;
                    };
                }
            }
        }
        return true;
}
runTestCase(testcase);
