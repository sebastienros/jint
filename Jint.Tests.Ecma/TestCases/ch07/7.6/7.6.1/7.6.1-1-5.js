/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-5.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: finally, return, void
 */


function testcase(){      
        var tokenCodes  = { 
            finally: 0,
            return: 1,
            void: 2
        };
        var arr = [ 
            'finally', 
            'return', 
            'void'
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
