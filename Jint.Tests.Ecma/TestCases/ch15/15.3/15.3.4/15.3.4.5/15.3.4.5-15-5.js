/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-15-5.js
 * @description Function.prototype.bind - The [[Configurable]] attribute of length property in F set as false
 */


function testcase() {

        var canConfigurable = false;
        var hasProperty = false;
        function foo() { }
        var obj = foo.bind({});
        hasProperty = obj.hasOwnProperty("length");
        delete obj.caller;
        canConfigurable = !obj.hasOwnProperty("length");
        return hasProperty && !canConfigurable;
    }
runTestCase(testcase);
