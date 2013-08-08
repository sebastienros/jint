/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.5/15.2.3.5-4-18.js
 * @description Object.create - an enumerable inherited data property in 'Properties' is not defined in 'obj' (15.2.3.7 step 3)
 */


function testcase() {

        var proto = {};

        proto.prop = {};

        var ConstructFun = function () { };
        ConstructFun.prototype = proto;
        var child = new ConstructFun();

        var newObj = Object.create({}, child);

        return !newObj.hasOwnProperty("prop");
    }
runTestCase(testcase);
