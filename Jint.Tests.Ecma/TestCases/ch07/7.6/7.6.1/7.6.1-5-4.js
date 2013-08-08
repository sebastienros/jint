/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch07/7.6/7.6.1/7.6.1-5-4.js
 * @description Allow reserved words as property names at object initialization, accessed via indexing: new, var, catch
 */


function testcase() {
        var tokenCodes  = { 
            new: 0,
            var: 1,
            catch: 2
        };
        var arr = [
            'new', 
            'var', 
            'catch'
        ]; 
        for (var i = 0; i < arr.length; i++) {
            if (tokenCodes[arr[i]] !== i) {
                return false;
            };
        }
        return true;
    }
runTestCase(testcase);
