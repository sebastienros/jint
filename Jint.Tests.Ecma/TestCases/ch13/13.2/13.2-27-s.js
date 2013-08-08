/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-27-s.js
 * @description StrictMode - enumerating over a function object looking for 'arguments' fails outside of the function
 * @onlyStrict
 */



function testcase() {
        function foo () {"use strict";}
        
        for (var tempIndex in foo) {
            if (tempIndex === "arguments") {
                return false;
            }
        }
        return true;
}
runTestCase(testcase);