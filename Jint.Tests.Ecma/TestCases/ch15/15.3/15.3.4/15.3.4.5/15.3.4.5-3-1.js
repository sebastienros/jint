/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.3/15.3.4/15.3.4.5/15.3.4.5-3-1.js
 * @description Function.prototype.bind - each arg is defined in A in list order
 */


function testcase() {

        var foo = function (x, y) {
            return new Boolean((x + y) === "ab" && arguments[0] === "a" &&
                arguments[1] === "b" && arguments.length === 2);
        };

        var obj = foo.bind({}, "a", "b");
        return obj()==true;
    }
runTestCase(testcase);
