/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-10.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: in, try, class
 */


function testcase(){      
        var tokenCodes  = { 
            in: 0, 
            try: 1,
            class: 2
        };
        var arr = [
            'in', 
            'try',
            'class'
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
