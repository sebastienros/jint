/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-8.js
 * @description with introduces scope - var initializer sets like named property
 */


function testcase() {
  var o = {foo: 42};

  with (o) {
    var foo = "set in with";
  }

  return o.foo === "set in with";
 }
runTestCase(testcase);
