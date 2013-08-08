/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-6.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: continue, for, switch
 */


function testcase(){      
        var tokenCodes  = { 
            continue: 0, 
            for: 1, 
            switch: 2
        };
        var arr = [
            'continue', 
            'for',
            'switch'
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
