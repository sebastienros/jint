/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-3-15.js
 * @description Allow reserved words as property names by index assignment,verified with hasOwnProperty: package, protected, static
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes['package'] = 0;
        tokenCodes['protected'] = 1;
        tokenCodes['static'] = 2;
        var arr = [
            'package',
            'protected',
            'static'   
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
