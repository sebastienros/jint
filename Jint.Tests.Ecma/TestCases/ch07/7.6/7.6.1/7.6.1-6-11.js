/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-6-11.js
 * @description Allow reserved words as property names by dot operator assignment, accessed via indexing: enum, extends, super
 */


function testcase() {
        var tokenCodes  = {};
        tokenCodes.enum = 0;
        tokenCodes.extends = 1;
        tokenCodes.super = 2;
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
