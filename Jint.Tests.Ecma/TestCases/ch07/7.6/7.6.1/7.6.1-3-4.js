/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-3-4.js
 * @description Allow reserved words as property names by index assignment,verified with hasOwnProperty: new, var, catch
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes['new'] = 0;
        tokenCodes['var'] = 1;
        tokenCodes['catch'] = 2;
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
