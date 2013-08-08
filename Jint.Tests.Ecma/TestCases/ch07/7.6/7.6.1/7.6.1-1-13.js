/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-13.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: implements, let, private
 */


function testcase(){      
        var tokenCodes  = { 
            implements: 0,
            let: 1,
            private: 2
        };
        var arr = [
            'implements',
            'let',
            'private'
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
