/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-12.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: const, export, import
 */


function testcase() {
        var tokenCodes = {
            const : 0,
            export: 1,
            import: 2
        };
        var arr = [
            'const',
            'export',
            'import'
        ]; 
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
