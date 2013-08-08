/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-3-9.js
 * @description Allow reserved words as property names by index assignment,verified with hasOwnProperty: if, throw, delete
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes['if'] = 0;
        tokenCodes['throw'] = 1;
        tokenCodes['delete'] = 2;
        var arr = [
            'if',
            'throw',
            'delete'
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
