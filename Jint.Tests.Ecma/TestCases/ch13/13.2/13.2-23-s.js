/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch13/13.2/13.2-23-s.js
 * @description StrictMode - enumerating over a function object looking for 'caller' fails outside of the function
 * @onlyStrict
 */



function testcase() {
        function foo () {"use strict";}
        for (var tempIndex in foo) {
            if (tempIndex === "caller") {
                return false;
            }
        }
        return true;
}
runTestCase(testcase);