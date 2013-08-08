/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-4.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: new, var, catch
 */


function testcase(){      
        var tokenCodes  = { 
            new: 0,
            var: 1,
            catch: 2
        };
        var arr = [
            'new', 
            'var', 
            'catch'
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
