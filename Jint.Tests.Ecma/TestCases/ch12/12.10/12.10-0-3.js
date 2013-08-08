/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-3.js
 * @description with introduces scope - that is captured by function expression
 */


function testcase() {
  var o = {prop: "12.10-0-3 before"};
  var f;

  with (o) {
    f = function () { return prop; }
  }
  o.prop = "12.10-0-3 after";
  return f()==="12.10-0-3 after"
 }
runTestCase(testcase);
