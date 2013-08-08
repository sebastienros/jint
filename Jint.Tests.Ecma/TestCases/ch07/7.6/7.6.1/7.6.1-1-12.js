/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-12.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: const, export, import
 */


function testcase(){      
        var tokenCodes  = { 
            const: 0,
            export: 1,
            import: 2
        };
        var arr = [
            'const',
            'export',
            'import'
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
