/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-16.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: undeefined, NaN, Infinity
 */


function testcase(){      
        var tokenCodes  = { 
            undefined: 0,
            NaN: 1,
            Infinity: 2
        };
        var arr = [
            'undefined',
            'NaN',
            'Infinity'
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
