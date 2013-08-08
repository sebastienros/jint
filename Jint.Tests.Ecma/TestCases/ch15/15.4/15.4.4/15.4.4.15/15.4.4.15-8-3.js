/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-3.js
 * @description Array.prototype.lastIndexOf must return correct index(string)
 */


function testcase() {
  var obj = {toString:function (){return "false"}};
  var szFalse = "false";
  var a = new Array(szFalse, "false","false1",undefined,0,false,null,1,obj,0);
  if (a.lastIndexOf("false") === 1) 
  {
    return true;
  }
 }
runTestCase(testcase);
