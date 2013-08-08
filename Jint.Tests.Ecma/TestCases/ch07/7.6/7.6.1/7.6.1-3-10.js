/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-3-10.js
 * @description Allow reserved words as property names by index assignment,verified with hasOwnProperty: in, try, class
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes['in'] = 0;
        tokenCodes['try'] = 1;
        tokenCodes['class'] = 2;
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
