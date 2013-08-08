/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-1-11.js
 * @description Allow reserved words as property names at object initialization, verified with hasOwnProperty: enum, extends, super
 */


function testcase(){      
        var tokenCodes  = { 
            enum: 0,
            extends: 1,
            super: 2
        };
        var arr = [
            'enum',
            'extends',
            'super'
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
