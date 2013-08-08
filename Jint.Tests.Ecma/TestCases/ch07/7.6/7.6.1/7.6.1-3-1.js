/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-3-1.js
 * @description Allow reserved words as property names by index assignment,verified with hasOwnProperty: null, true, false
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes['null'] = 0;
	    tokenCodes['true'] = 1;
	    tokenCodes['false'] = 2;
        var arr = [
            'null',
            'true',
            'false'
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
