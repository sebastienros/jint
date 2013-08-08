/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-11.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: enum, extends, super
 */


function testcase() {
        var tokenCodes = {
            enum: 0,
            extends: 1,
            super: 2
        };
        var arr = [
            'enum',
            'extends',
            'super'
        ];  
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
